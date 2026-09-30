using ReadyM.Api.DI;
using Microsoft.Extensions.Logging;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Multiplayer.ECS.Archetypes;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Client.Archetypes;
using ReadyM.SDK.Services;
using ReadyM.SDK.Client.Mapping;
using ReadyM.SDK.Client.Entities;
using ReadyM.SDK.Entities;
#if NET
using ReadyM.SDK.Chunks;
using ReadyM.SDK.Client.Chunks;
#endif

namespace ReadyM.SDK.Client;

public static class DependencyInjectionExtensions
{
    /// Register data mappings declared in all loaded mods.
    /// A game calls this once its mods are loaded and before anything reaches a sync point.
    public static void ApplyShapeMappings(this IDependencyContainer container)
    {
        var mappings = new ShapeMappingRegistry();

        foreach (var declared in container.ResolveAll<IShapeMappings>())
            declared.Register(mappings);

        SyncExtensions.Use(mappings);
    }

    public static void ApplyArchetypeExtensions(this IDependencyContainer container)
        => new ArchetypeExtensionRegistration(
            container.Resolve<DefaultWorldArchetypeRegistration>(),
            container.Resolve<DefaultAreaArchetypeRegistration>(),
            container.Resolve<DefaultPlayerArchetypeRegistration>(),
            container.Resolve<DefaultCellArchetypeRegistration>(),
            container.ResolveAll<IArchetypeShapeBindings>(),
            container.Resolve<ILogger>()
        ).Register(container.Resolve<Store>());

    public static void RegisterReadyMSdk(this IDependencyContainer container)
    {
        CreateHandlerRegistry.Use(container);
        ServiceRegistry.RegisterAll(container);

        container.RegisterSingleton<IEntities, ClientEntities>();
        container.RegisterSingleton<IEntityApi, ClientEntityApi>();
#if NET
        container.RegisterSingleton<IChunkSource, ClientChunkSource>();
#endif
    }
}