using System;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using ReadyM.Api.Interop;

namespace ReadyM.Api.Mapping.Events;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
internal static class NativeMappedEventManagerBindings
{
    public delegate void RegisterNativeGameEventHandlerDelegate(IntPtr managerPtr, int eventId, ClosureTrampoline1 callback);

    public static void RegisterNativeGameEventHandler(IntPtr managerPtr, int eventId, ClosureTrampoline1 callback)
    {
        var manager = (NativeMappedEventManager)GCHandle.FromIntPtr(managerPtr).Target!;
        manager.RegisterNativeGameEventHandler(eventId, callback);
    }

    /// <returns>The event's run-locally <see cref="GameEventResult"/>, as a byte.</returns>
    public delegate byte NotifyEcsIfApplicableDelegate(IntPtr managerPtr, int eventId, IntPtr data);

    public static byte NotifyEcsIfApplicable(IntPtr managerPtr, int eventId, IntPtr data)
    {
        var manager = (NativeMappedEventManager)GCHandle.FromIntPtr(managerPtr).Target!;
        return manager.NotifyEcsIfApplicable(eventId, data);
    }

    /// <returns>The event's invoke-in-game <see cref="GameEventResult"/>, as a byte.</returns>
    public delegate byte InvokeInGameIfApplicableDelegate(IntPtr managerPtr, int eventId, IntPtr data);

    public static byte InvokeInGameIfApplicable(IntPtr managerPtr, int eventId, IntPtr data)
    {
        var manager = (NativeMappedEventManager)GCHandle.FromIntPtr(managerPtr).Target!;
        return manager.InvokeInGameIfApplicable(eventId, data);
    }

    /// <returns>The event's run-locally <see cref="GameEventResult"/>, as a byte, without notifying anyone.</returns>
    public delegate byte CanGameEventRunLocallyDelegate(IntPtr managerPtr, int eventId, IntPtr data);

    public static byte CanGameEventRunLocally(IntPtr managerPtr, int eventId, IntPtr data)
    {
        var manager = (NativeMappedEventManager)GCHandle.FromIntPtr(managerPtr).Target!;
        return manager.CanGameEventRunLocally(eventId, data);
    }
}
