using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using ReadyM.Api.DI;
using ReadyM.Api.Multiplayer.RPC;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// Holds what a loaded assembly generated for network events, so the SDK can put it in the container without
/// naming any of it. Two kinds: the contract an event's own Common project declares, once per event, and the
/// handler set a mod declares, once per event it handles.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkEventRegistry
{
    private static readonly ConcurrentDictionary<Type, Action<IDependencyContainer, INetworkEventCoordinators>>
        Contracts = new();

    private static readonly ConcurrentDictionary<Type, Handling> Handlers = new();

    /// <summary>Called by generated code in the project that declares the event.</summary>
    public static void Declare<T>(INetworkEventContract<T> contract) where T : struct, INetworkEvent
        => Contracts[typeof(T)] = (container, coordinators) => coordinators.Register(container, contract);

    /// <summary>Called by generated code in each mod that handles the event.</summary>
    public static void Handle<THandlers, T>(RpcSide side)
        where THandlers : class, INetworkEventHandlers<T>
        where T : struct, INetworkEvent
        => Handlers[typeof(THandlers)] = new Handling(
            side, container => container.RegisterSingleton<INetworkEventHandlers<T>, THandlers>());

    /// <summary>
    /// Registers this side's handler sets and a coordinator per declared event. Every event gets one, handled
    /// here or not: an event this side has nothing to say about is still forwarded, and still refused.
    /// </summary>
    public static void RegisterAll(
        IDependencyContainer container, RpcSide side, INetworkEventCoordinators coordinators)
    {
        foreach (var handling in Handlers.Values)
            if (handling.Side == side)
                handling.Register(container);

        foreach (var declared in Contracts.Values)
            declared(container, coordinators);
    }

    private readonly struct Handling(RpcSide side, Action<IDependencyContainer> register)
    {
        public RpcSide Side { get; } = side;

        public Action<IDependencyContainer> Register { get; } = register;
    }
}
