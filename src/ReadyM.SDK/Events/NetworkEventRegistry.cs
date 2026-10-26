using System.Collections.Concurrent;
using System.ComponentModel;
using ReadyM.Api.DI;
using ReadyM.Api.Multiplayer.RPC;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// Holds the dispatch classes a loaded assembly generated, so the SDK can put them in the container without
/// naming any of them. The same shape as the RPC handler registry, for the same reason: a mod declares its
/// events and handlers, and nothing else.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkEventRegistry
{
    private static readonly ConcurrentDictionary<Type, Declaration> Declared = new();

    /// <summary>Called by generated code for one event's dispatch.</summary>
    public static void Declare<TDispatch, TEvent>(RpcSide side)
        where TDispatch : class, INetworkEventDispatch<TEvent>
        where TEvent : struct, INetworkEvent
        => Declared[typeof(TDispatch)] = new Declaration(
            side,
            container => container.RegisterSingleton<INetworkEventDispatch<TEvent>, TDispatch>());

    /// <summary>Registers every dispatch declared for one side, which is also what starts it listening.</summary>
    public static void RegisterAll(IDependencyContainer container, RpcSide side)
    {
        foreach (var declaration in Declared.Values)
            if (declaration.Side == side)
                declaration.Register(container);
    }

    private readonly struct Declaration(RpcSide side, Action<IDependencyContainer> register)
    {
        public RpcSide Side { get; } = side;

        public Action<IDependencyContainer> Register { get; } = register;
    }
}
