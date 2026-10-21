using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using LiteNetLib;
using LiteNetLib.Utils;
using ReadyM.Api.Interop;
using ReadyM.Api.Multiplayer;
using ReadyM.Api.Multiplayer.ConflictResolution;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Api.Multiplayer.Interning;
using ReadyM.Api.Multiplayer.Interop;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using Yooni.Native.Container;

namespace ReadyM.Relay.Server.Sdk.Ecs;

/// <summary>
/// The replication of mod components. Consumes <see cref="ModNetworkedComponentRegistry"/> to create and pin each
/// networked component's five networked pointers, and is bound to the AOT side's ModNetworkedComponentRegistration,
/// which it carries them to.
/// </summary>
internal sealed class ModComponentReplication : IDisposable
{
    /// <summary>What it calls on the AOT side: ModNetworkedComponentRegistration's methods.</summary>
    public struct NativeBinding
    {
        public required IntPtr RegisterModComponent;   // RegisterModNetworkedComponentDelegate
        public required IntPtr Complete;               // CompleteModComponentRegistrationDelegate
    }

    private sealed class CreateInfoCallback(ModComponentReplication owner) : INetworkedComponentRegistryCallback
    {
        public void AcceptComponent<T>(INetworkedComponentRegistry registry, T defaultValue = default)
            where T : struct, INetworkedComponent
            => owner._modComponentInfos.Add(
                (owner.CreateModNetworkedComponentInfo<T>(), typeof(T).FullName!, registry.GetNetworkedComponentDeliveryMethod<T>()));

        public void AcceptModComponent(INetworkedComponentRegistry registry, ModComponentInfo info, string typeFullName)
            => throw new NotSupportedException(
                $"The managed mod side registers typed components only, so {typeFullName} cannot arrive as a mod component here.");
    }

    // Keeps the serialization delegates alive - their IntPtrs live in AOT memory.
    private readonly PinnedDelegateStore _delegateStore = new();

    // In registration order, which is the order they cross in
    private readonly List<(ModNetworkedComponentInfo Info, string TypeFullName, DeliveryMethod DeliveryMethod)> _modComponentInfos = [];

    // FixMe(2026-10-18-twins-m5): set in Bind step 5 after this exists. They become declared dependencies.
    internal IChangeTrackingStore? ChangeTracking { get; set; }

    internal IInternedStringIndex? InternedStringIndex { get; set; }

    public ModComponentReplication(ModNetworkedComponentRegistry registry)
    {
        registry.Accept(new CreateInfoCallback(this));
    }

    /// <summary>
    /// Carries every component's info and delivery method to ModNetworkedComponentRegistration, in registration
    /// order, then completes it.
    /// </summary>
    public void Bind(NativeBinding binding)
    {
        var registerModComponent = Marshal.GetDelegateForFunctionPointer<RegisterModNetworkedComponentDelegate>(binding.RegisterModComponent);

        foreach (var (info, typeFullName, deliveryMethod) in _modComponentInfos)
            registerModComponent(info, new NativeString256(typeFullName, false), deliveryMethod);

        Marshal.GetDelegateForFunctionPointer<CompleteModComponentRegistrationDelegate>(binding.Complete)();
    }

    // Null when no index was handed over: a default scope would clear whichever index the thread already has
    private InternedStrings.Scope? EnterInternedStrings()
        => InternedStringIndex is { } index ? InternedStrings.Enter(index) : null;

    /// <summary>The five networked pointers of T. The interned check is the twin's, at registration.</summary>
    private unsafe ModNetworkedComponentInfo CreateModNetworkedComponentInfo<T>()
        where T : struct, INetworkedComponent
    {
        var writer = new NetDataWriter();
        var readBuffer = new byte[1024 * 1024];
        var reader = new NetDataReader(readBuffer);

        // Stands in for the component when the AOT side has no entity to apply to and only needs
        // the byte count. Per-registration, and the serialization callbacks never run concurrently
        // for one component type.
        var scratch = new T[1];

        // The array is the mod runtime's own, so a ref into it is GC-tracked and stays valid.
        // Nothing here ever hands an address back across the boundary.
        ref T Slot(IntPtr heapSelf, int index)
        {
            if (heapSelf == IntPtr.Zero)
                return ref scratch[0];

            var heap = (TypedComponentHeap<T>)GCHandle.FromIntPtr(heapSelf).Target!;
            return ref heap.GetRef(index);
        }

        var writeSnapshotDelegate = new WriteSnapshotDelegate((heapSelf, index, buffer, bufferSize) =>
        {
            using var interned = EnterInternedStrings();

            writer.Reset();
            var data = Slot(heapSelf, index);
            writer.Put(data);
            var bytes = writer.Data;

            if (bytes.Length > bufferSize)
                throw new InvalidOperationException($"Buffer too small for snapshot of {typeof(T).Name}: need {bytes.Length} bytes, have {bufferSize} bytes");

            Marshal.Copy(bytes, 0, (IntPtr)buffer, bytes.Length);
            return writer.Length;
        });

        var readSnapshotDelegate = new ReadSnapshotDelegate((heapSelf, index, buffer, bufferSize) =>
        {
            using var interned = EnterInternedStrings();

            // TODO: Replace with a span
            if (bufferSize > readBuffer.Length)
                throw new InvalidOperationException($"Buffer too small for snapshot of {typeof(T).Name}: need {bufferSize} bytes, have {readBuffer.Length} bytes");

            Marshal.Copy((IntPtr)buffer, readBuffer, 0, bufferSize);
            reader.SetPosition(0);
            Slot(heapSelf, index) = reader.Get<T>();
            return reader.Position;
        });

        var writeDeltaDelegate = new WriteDeltaDelegate((heapSelf, index, buffer, bufferSize) =>
        {
            using var interned = EnterInternedStrings();

            ref var data = ref Slot(heapSelf, index);

            if (!data.IsDirty)
                return 0;

            writer.Reset();
            data.WriteDelta(writer);
            data.ClearDirty();

            var bytes = writer.Data;

            if (writer.Length > bufferSize)
                throw new InvalidOperationException($"Buffer too small for snapshot of {typeof(T).Name}: need {bytes.Length} bytes, have {bufferSize} bytes");

            Marshal.Copy(bytes, 0, (IntPtr)buffer, writer.Length);
            return writer.Length;
        });

        var readDeltaDelegate = new ReadDeltaDelegate((heapSelf, index, buffer, bufferSize, clearDirty, entityId, currentTime, lastObservedTime, resolveConflicts) =>
        {
            using var interned = EnterInternedStrings();

            // TODO: Replace with a span
            if (bufferSize > readBuffer.Length)
                throw new InvalidOperationException($"Buffer too small for snapshot of {typeof(T).Name}: need {bufferSize} bytes, have {readBuffer.Length} bytes");

            Marshal.Copy((IntPtr)buffer, readBuffer, 0, bufferSize);
            reader.SetPosition(0);
            ref var data = ref Slot(heapSelf, index);

            if (resolveConflicts != 0 && heapSelf != IntPtr.Zero && ChangeTracking is { } changes)
            {
                using (ComponentWriteContext.EnterServerApplyDelta(currentTime, lastObservedTime, changes))
                    data.ReadDeltaTracking(reader, entityId);
            }
            else
            {
                data.ReadDelta(reader);
            }

            if (clearDirty == 1)
            {
                data.ClearDirty();
            }

            return reader.Position;
        });

        var changedFromApiDelegate = new ChangedFromApiDelegate((heapSelf, index) =>
        {
            ref var data = ref Slot(heapSelf, index);
            return data.ChangedFromApi ? (byte)1 : (byte)0;
        });

        return new ModNetworkedComponentInfo
        {
            WriteSnapshot = _delegateStore.PinDelegate(writeSnapshotDelegate),
            ReadSnapshot = _delegateStore.PinDelegate(readSnapshotDelegate),
            WriteDelta = _delegateStore.PinDelegate(writeDeltaDelegate),
            ReadDelta = _delegateStore.PinDelegate(readDeltaDelegate),
            ChangedFromApi = _delegateStore.PinDelegate(changedFromApiDelegate),
        };
    }

    public void Dispose()
    {
        _delegateStore.Dispose();
    }
}
