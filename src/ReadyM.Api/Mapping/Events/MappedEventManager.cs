using System;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Helpers;

namespace ReadyM.Api.Mapping.Events;

internal class MappedEventManager(
    DataSideChannel sideChannel,
    GameEventContextRegistry contexts,
    ILogger logger
) : IMappedEventManager
{
    protected readonly EventQueue incomingEcsEventQueue = new(logger);
    protected readonly EventQueue incomingGameEventQueue = new(logger);

    public void RegisterEcsEventHandler<TEvent>(Action<TEvent> handler)
        where TEvent : struct
        => incomingEcsEventQueue.RegisterHandler(handler);

    public void RegisterEcsEventHandler<TEvent, TArg>(Action<TEvent, TArg> handler, TArg arg)
        where TEvent : struct
        => incomingEcsEventQueue.RegisterHandler(handler, arg);

    public void RegisterEcsEventHandler<TEvent, TArg0, TArg1>(Action<TEvent, TArg0, TArg1> handler, TArg0 arg0, TArg1 arg1)
        where TEvent : struct
        => incomingEcsEventQueue.RegisterHandler(handler, arg0, arg1);

    public void RegisterEcsEventHandler<TEvent, TArg0, TArg1, TArg2>(Action<TEvent, TArg0, TArg1, TArg2> handler, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        where TEvent : struct
        => incomingEcsEventQueue.RegisterHandler(handler, arg0, arg1, arg2);

    public void RegisterGameEventHandler<TEvent>(Action<TEvent> handler)
        where TEvent : struct
        => incomingGameEventQueue.RegisterHandler(handler);

    public void RegisterGameEventHandler<TEvent, TArg>(Action<TEvent, TArg> handler, TArg arg)
        where TEvent : struct
        => incomingGameEventQueue.RegisterHandler(handler, arg);

    public void RegisterGameEventHandler<TEvent, TArg0, TArg1>(Action<TEvent, TArg0, TArg1> handler, TArg0 arg0, TArg1 arg1)
        where TEvent : struct
        => incomingGameEventQueue.RegisterHandler(handler, arg0, arg1);

    public void InvokeInGameAndNotifyEcs<TEvent>(in TEvent ev)
        where TEvent : struct, IGameEvent
    {
        NotifyEcsIfApplicable(ev);
        InvokeInGameIfApplicable(ev);
    }

    /// <inheritdoc/>
    public GameEventResult NotifyEcsIfApplicable<TEvent>(in TEvent ev)
        where TEvent : struct, IGameEvent
    {
        // NOTE: The echo rule: the game code the ECS is playing is not sent back, and runs.
        if (sideChannel.HasData<PropagatingToGameScope<TEvent>>())
            return GameEventResult.RunAll;

        if (ev.CanGameEventNotifyEcs(contexts) == GameEventNotifyResult.Notify)
        {
            using (sideChannel.PushScope<PropagatingToEcsScope<TEvent>>())
            {
                incomingEcsEventQueue.Invoke(ev);
            }
        }

        return ev.CanGameEventRunLocally(contexts);
    }

    /// <inheritdoc/>
    public GameEventResult InvokeInGameIfApplicable<TEvent>(in TEvent ev)
        where TEvent : struct, IGameEvent
    {
        // NOTE: The echo rule: what the game is sending is not played back to it.
        if (sideChannel.HasData<PropagatingToEcsScope<TEvent>>())
            return GameEventResult.DontRun;

        var result = ev.CanEcsInvokeGameEvent(contexts);
        if (result.Runs())
        {
            using (sideChannel.PushScope<PropagatingToGameScope<TEvent>>())
            {
                incomingGameEventQueue.Invoke(ev);
            }
        }

        return result;
    }

    public GameEventNotifyResult CanGameEventNotifyEcs<TEvent>(in TEvent ev)
        where TEvent : struct, IGameEvent
        => sideChannel.HasData<PropagatingToGameScope<TEvent>>()
            ? GameEventNotifyResult.DontNotify
            : ev.CanGameEventNotifyEcs(contexts);

    public GameEventResult CanGameEventRunLocally<TEvent>(in TEvent ev)
        where TEvent : struct, IGameEvent
        => sideChannel.HasData<PropagatingToGameScope<TEvent>>()
            ? GameEventResult.RunAll
            : ev.CanGameEventRunLocally(contexts);

    public GameEventResult CanEcsInvokeGameEvent<TEvent>(in TEvent ev)
        where TEvent : struct, IGameEvent
        => sideChannel.HasData<PropagatingToEcsScope<TEvent>>()
            ? GameEventResult.DontRun
            : ev.CanEcsInvokeGameEvent(contexts);
}
