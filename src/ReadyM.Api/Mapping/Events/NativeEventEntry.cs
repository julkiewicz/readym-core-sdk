using System;
using System.Runtime.CompilerServices;
using ReadyM.Api.Interop;

namespace ReadyM.Api.Mapping.Events;

/// <summary>One native event's typed way into the manager, found by the event's native id.</summary>
internal abstract class NativeEventEntry
{
    public abstract GameEventResult NotifyEcsIfApplicable(IMappedEventManager manager, IntPtr data);
    public abstract GameEventResult InvokeInGameIfApplicable(IMappedEventManager manager, IntPtr data);
    public abstract void RegisterNativeGameEventHandler(IMappedEventManager manager, ClosureTrampoline1 callback);
}

internal sealed unsafe class NativeEventEntry<TEvent> : NativeEventEntry
    where TEvent : struct, IGameEvent
{
    public override GameEventResult NotifyEcsIfApplicable(IMappedEventManager manager, IntPtr data)
        => manager.NotifyEcsIfApplicable(Unsafe.Read<TEvent>((void*)data));

    public override GameEventResult InvokeInGameIfApplicable(IMappedEventManager manager, IntPtr data)
        => manager.InvokeInGameIfApplicable(Unsafe.Read<TEvent>((void*)data));

    public override void RegisterNativeGameEventHandler(IMappedEventManager manager, ClosureTrampoline1 callback)
        => manager.RegisterGameEventHandler<TEvent, ClosureTrampoline1>(InvokeNative, callback);

    private static void InvokeNative(TEvent ev, ClosureTrampoline1 callback)
        => callback.Invoke((IntPtr)Unsafe.AsPointer(ref ev));
}
