using System.Reflection;
using Microsoft.Extensions.Logging;
using ReadyM.Api.DI;
using ReadyM.Api.Multiplayer.RPC;

namespace ReadyM.SDK.Events;

/// <summary>The networked event contracts and handlers the loaded assemblies declare.</summary>
internal sealed class NetworkedEventRegistry(ILogger logger) : INetworkedEventRegistry
{
    /// <summary>The generated classes, and their static methods, that declare an assembly's events and handlers.</summary>
    private static readonly (string Type, string Method)[] GeneratedDeclarations =
    [
        ("ReadyM.SDK.Generated.NetworkedEventDeclarations", "DeclareAll"),
        ("ReadyM.SDK.Generated.NetworkedEventHandlerDeclarations", "DeclareAll"),
    ];

    private readonly Dictionary<Type, ContractEntry> _contracts = [];

    private readonly Dictionary<Type, HandlersEntry> _handlers = [];

    public void Declare<T>(INetworkedEventContract<T> contract) where T : struct, INetworkedEvent
        => _contracts[typeof(T)] = new ContractEntry
        {
            Event = typeof(T),
            RegisterCoordinator = (container, registration) => registration.Register(container, contract),
        };

    public void AddHandlers<THandlers, T>(RpcSide side)
        where THandlers : class, INetworkedEventHandlers<T>
        where T : struct, INetworkedEvent
        => _handlers[typeof(THandlers)] = new HandlersEntry
        {
            Side = side,
            Event = typeof(T),
            Register = container => container.RegisterSingleton<INetworkedEventHandlers<T>, THandlers>(),
        };

    /// <summary>Runs the generated declarations of every assembly that has any. Declaring twice is harmless.</summary>
    public void DeclareGenerated(IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies.Distinct())
        {
            try
            {
                if (DeclareGenerated(assembly))
                    logger.LogDebug("Declared networked events from {Assembly}", assembly.GetName().Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to declare networked events from {Assembly}", assembly.GetName().Name);
            }
        }
    }

    /// <summary>
    /// Registers this side's handlers, and a coordinator for every declared event, handled on this side or not.
    /// </summary>
    public void RegisterAll(IDependencyContainer container, RpcSide side, INetworkedEventCoordinatorRegistration registration)
    {
        var handlers = _handlers.Values.Where(entry => entry.Side == side).ToList();

        foreach (var entry in handlers)
            entry.Register(container);

        // An event declared in an assembly loaded into two contexts gets one coordinator, not one per copy
        foreach (var copies in _contracts.Values.GroupBy(entry => Identity(entry.Event)))
            CopyHandledHere(copies, handlers).RegisterCoordinator(container, registration);
    }

    private bool DeclareGenerated(Assembly assembly)
    {
        var declared = false;

        foreach (var (type, method) in GeneratedDeclarations)
        {
            if (assembly.GetType(type, throwOnError: false)?.GetMethod(method, BindingFlags.Public | BindingFlags.Static)
                is not { } declare)
                continue;

            declare.Invoke(null, [this]);
            declared = true;
        }

        return declared;
    }

    /// <summary>
    /// The copy of an event whose type this side's handlers name, so the coordinator and its handlers agree on
    /// the type. Any copy will do for an event no handler here names.
    /// </summary>
    private static ContractEntry CopyHandledHere(IEnumerable<ContractEntry> copies, List<HandlersEntry> handlers)
    {
        var all = copies.ToList();

        foreach (var copy in all)
            if (handlers.Any(entry => entry.Event == copy.Event))
                return copy;

        return all[0];
    }

    /// <summary>The same for every copy of one event: its name, and the name of the assembly that declared it.</summary>
    private static string Identity(Type declared) => declared.FullName + ", " + declared.Assembly.GetName().Name;

    private struct ContractEntry
    {
        public Type Event;
        public Action<IDependencyContainer, INetworkedEventCoordinatorRegistration> RegisterCoordinator;
    }

    private struct HandlersEntry
    {
        public RpcSide Side;
        public Type Event;
        public Action<IDependencyContainer> Register;
    }
}
