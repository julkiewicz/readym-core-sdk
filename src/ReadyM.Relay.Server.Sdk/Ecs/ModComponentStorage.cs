using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using Microsoft.Extensions.Logging;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Interop;
using ReadyM.Api.Multiplayer.Interop;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using Yooni.Native.Container;

namespace ReadyM.Relay.Server.Sdk.Ecs;

/// <summary>
/// The storage of mod components. Consumes <see cref="ModAllComponentRegistry"/> to create and pin each component's
/// storage pointers, owns the heaps they allocate, and is bound to the AOT side's ModComponentsRegistration, which it
/// carries them to.
/// <para>
/// The AOT side calls AllocHeap each time a new archetype needs a heap. All allocated heaps are tracked here and
/// disposed in bulk on shutdown.
/// </para>
/// </summary>
internal sealed class ModComponentStorage : IDisposable
{
    /// <summary>What it calls on the AOT side: ModComponentsRegistration's methods.</summary>
    public struct NativeBinding
    {
        public required IntPtr RegisterModComponent;   // RegisterModComponentDelegate
        public required IntPtr Complete;               // CompleteModComponentRegistrationDelegate
    }

    private sealed class CreateInfoCallback(ModComponentStorage owner, ILogger logger) : IAllComponentRegistryCallback
    {
        public void AcceptComponent<T>(IAllComponentRegistry registry, T defaultValue = default)
            where T : struct, IComponent
        {
            if (Unsafe.SizeOf<T>() > 256)
                logger.LogWarning("Component {Component} is {Stride} bytes which exceeds 256 bytes", typeof(T).FullName, Unsafe.SizeOf<T>());

            owner._modComponentInfos.Add((owner.CreateModComponentInfo<T>(), typeof(T).FullName!));
        }

        public void AcceptModComponent(IAllComponentRegistry registry, ModComponentInfo info, string typeFullName)
            => throw new NotSupportedException(
                $"The managed mod side registers typed components only, so {typeFullName} cannot arrive as a mod component here.");
    }

    // Keeps factory delegates alive - their IntPtrs live in AOT memory.
    private readonly PinnedDelegateStore _delegateStore = new();

    // All heaps ever allocated via any factory, for bulk disposal on shutdown.
    private readonly List<IDisposable> _allHeaps = [];
    private readonly Lock _heapsLock = new();

    // In registration order, which is the order they cross in
    private readonly List<(ModComponentInfo Info, string TypeFullName)> _modComponentInfos = [];

    public ModComponentStorage(ModAllComponentRegistry registry, ILogger logger)
    {
        registry.Accept(new CreateInfoCallback(this, logger));
    }

    /// <summary>Carries every component's info to ModComponentsRegistration, in registration order, then completes it.</summary>
    public void Bind(NativeBinding binding)
    {
        var registerModComponent = Marshal.GetDelegateForFunctionPointer<RegisterModComponentDelegate>(binding.RegisterModComponent);

        foreach (var (info, typeFullName) in _modComponentInfos)
            registerModComponent(info, new NativeString256(typeFullName, false));

        Marshal.GetDelegateForFunctionPointer<CompleteModComponentRegistrationDelegate>(binding.Complete)();
    }

    /// <summary>
    /// The storage pointers of T. The embedded AllocHeap delegate allocates a fresh TypedComponentHeap&lt;T&gt;
    /// each time an archetype needs one.
    /// </summary>
    private ModComponentInfo CreateModComponentInfo<T>() where T : struct
    {
        // Bind the factory to this T at registration time. Capturing via method group
        // means each call creates an independent delegate instance correctly specialized for that T.
        var factory = new AllocHeapDelegate(AllocHeapImpl<T>);

        return new ModComponentInfo
        {
            Stride = Unsafe.SizeOf<T>(),
            IsBlittable = IsBlittable<T>() ? (byte)1 : (byte)0,
            AllocHeap = _delegateStore.PinDelegate(factory),
        };
    }

    // -------------------------------------------------------------------------
    // Heap factory - called from AOT via function pointer
    // -------------------------------------------------------------------------

    private void AllocHeapImpl<T>(int capacity, IntPtr outPtr) where T : struct
    {
        var heap = new TypedComponentHeap<T>(capacity);

        lock (_heapsLock)
        {
            _allHeaps.Add(heap);
        }

        // outPtr is a caller-allocated AOTHeapPointers on the AOT stack.
        // Writing through the pointer is safe: the call is synchronous and the
        // AOT stack frame that owns the struct is alive for the duration.
        unsafe
        {
            *(AOTHeapPointers*)outPtr = heap.GetPointers();
        }
    }

    private static bool IsBlittable<T>() where T : struct
    {
        try
        {
            var h = GCHandle.Alloc(new T[1], GCHandleType.Pinned);
            h.Free();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Lifetime
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        // Dispose delegates first - stops any in-flight AllocHeap calls from
        // writing into heaps we are about to tear down.
        _delegateStore.Dispose();

        lock (_heapsLock)
        {
            foreach (var heap in _allHeaps)
                heap.Dispose();
            _allHeaps.Clear();
        }
    }

    ~ModComponentStorage() => Dispose();
}
