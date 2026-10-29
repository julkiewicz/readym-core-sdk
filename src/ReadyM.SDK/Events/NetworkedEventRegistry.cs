using System.Collections.Concurrent;
using System.ComponentModel;
using ReadyM.Api.DI;
using ReadyM.Api.Multiplayer.RPC;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// Holds what a loaded assembly generated for network events.
/// Two kinds:<br/>
/// - the contract an event's own Common project declares, once per event,<br/>
/// - and the handler set a mod declares, once per event it handles.<br/>
/// Used by generated code (NetworkedEventDeclarations).
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkedEventRegistry
{
    private static readonly ConcurrentDictionary<Type, Declaration> Contracts = new();

    private static readonly ConcurrentDictionary<Type, Handling> Handlers = new();

    /// <summary>Called by generated code in the project that declares the event.</summary>
    public static void Declare<T>(INetworkedEventContract<T> contract) where T : struct, INetworkedEvent
        => Contracts[typeof(T)] = new Declaration(typeof(T),
            (container, coordinators) => coordinators.Register(container, contract)
        );

    /// <summary>Called by generated code in each mod that handles the event.</summary>
    public static void Handle<THandlers, T>(RpcSide side)
        where THandlers : class, INetworkedEventHandlers<T>
        where T : struct, INetworkedEvent
        => Handlers[typeof(THandlers)] = new Handling(side, typeof(T),
            container => container.RegisterSingleton<INetworkedEventHandlers<T>, THandlers>()
        );

    /// <summary>
    /// Registers this side's handler sets and a coordinator per declared event. Every event gets one, handled
    /// here or not: an event this side has nothing to say about is still forwarded, and still refused.
    /// </summary>
    internal static void RegisterAll(IDependencyContainer container, RpcSide side, INetworkedEventCoordinatorRegistration registration)
    {
        var handlers = Handlers.Values.Where(handling => handling.Side == side).ToList();

        foreach (var handling in handlers)
            handling.Register(container);

        // One coordinator per event, not one per copy of the assembly that declared it.
        foreach (var copies in Contracts.Values.GroupBy(contract => Identity(contract.Event)))
            Chosen(copies, handlers).Register(container, registration);
    }

    /// <summary>
    /// The copy whose event type this side's handler sets name, so the coordinator and the handlers it runs
    /// agree on the type. Any copy will do for an event nothing here handles, because none of them differ.
    /// </summary>
    private static Declaration Chosen(IEnumerable<Declaration> copies, List<Handling> handlers)
    {
        var all = copies.ToList();

        foreach (var copy in all)
            if (handlers.Any(handling => handling.Event == copy.Event))
                return copy;

        return all[0];
    }

    /// <summary>
    /// What makes two copies of one event the same event: the name it was declared under, in the assembly
    /// that declared it. Neither says which context the copy was loaded into.
    /// </summary>
    private static string Identity(Type declared)
        => declared.FullName + ", " + declared.Assembly.GetName().Name;

    private readonly struct Declaration(Type declared, Action<IDependencyContainer, INetworkedEventCoordinatorRegistration> register)
    {
        public Type Event { get; } = declared;

        public Action<IDependencyContainer, INetworkedEventCoordinatorRegistration> Register { get; } = register;
    }

    private readonly struct Handling(RpcSide side, Type declared, Action<IDependencyContainer> register)
    {
        public RpcSide Side { get; } = side;

        public Type Event { get; } = declared;

        public Action<IDependencyContainer> Register { get; } = register;
    }
}