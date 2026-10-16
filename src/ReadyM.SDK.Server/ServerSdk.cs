using System.Runtime.CompilerServices;
using ReadyM.Api.DI;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Entities;
using ReadyM.Api.Multiplayer.RPC;
using ReadyM.SDK.Server.Systems;
using ReadyM.SDK.Services;

namespace ReadyM.SDK.Server;

/// <summary>What the SDK needs in place before any server-side mod runs.</summary>
internal static class ServerSdk
{
    private static readonly ConditionalWeakTable<IDependencyContainer, object> Bootstrapped = new();

    private static readonly ConditionalWeakTable<IComponentRegistry, object> Registered = new();

    /// <summary>
    /// Hands the relay every component the loaded shapes replicate or add to an archetype: their storage
    /// straight into <paramref name="registry"/>, and the registrations of every other concern into
    /// <paramref name="services"/>, which builds those registries once all mods have declared.
    /// </summary>
    public static void RegisterComponents(IComponentRegistry registry, IDependencyContainer services)
    {
        if (Registered.TryGetValue(registry, out _))
            return;

        Registered.Add(registry, new object());

        ServerReplication.RegisterAll(registry);
        ServerArchetypes.ApplyExtensions(registry);

        services.RegisterSingleton<INetworkedComponentRegistration>(new ReplicatedShapeRegistration());
    }

    /// <summary>The services and registries a mod resolves out of, done once for the container they share.</summary>
    public static void Use(IDependencyContainer services)
    {
        if (Bootstrapped.TryGetValue(services, out _))
            return;

        Bootstrapped.Add(services, new object());

        CreateHandlerRegistry.Use(services);

        var api = services.Resolve<IEntityApi>();

        ExtensionNativeInit.Use(api);
        ExtensionNativeDelete.Use(api);

        ServiceRegistry.RegisterAll(services);
        RpcHandlerRegistry.RegisterAll(services, RpcSide.Server);
        
        if (api is Entities.ServerEntityApi server)
            server.ApplyDeclaredTags();

        // TODO: Wire as a standard system somewhere, since this is based on the obsolete ModSystemBase
        ModSystemUpdates.Wire(services);
    }
}
