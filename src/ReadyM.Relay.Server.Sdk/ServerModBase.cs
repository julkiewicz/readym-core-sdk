using Microsoft.Extensions.Logging;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Loader;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using ReadyM.Relay.Server.Sdk.Ecs.Networked;

namespace ReadyM.Relay.Server.Sdk;

[Obsolete("Use the [ModEntry] attribute instead.")]
public abstract class ServerModBase
{
    protected IServerDependencyContainer Services { get; private set; } = null!;

    /// <summary>
    /// This mod's own <c>server</c> folder, which holds its assemblies and any config files.
    /// </summary>
    protected string ModDirectory { get; private set; } = null!;

    public void InitializeAot(IComponentRegistry registry)
    {
        RegisterComponents(registry);
    }

    /// <summary>
    /// Runs <see cref="RegisterComponents"/> against the networked registry, which takes the networked half of
    /// what it declares. Storage is handed the method directly, through <see cref="InitializeAot"/>.
    /// </summary>
    internal void RegisterNetworkedComponents(INetworkedComponentRegistry registry)
        => RegisterComponents(new NetworkedComponents(registry));

    /// <summary>The networked half of what a v0 mod declares: its local components are not networked.</summary>
    private sealed class NetworkedComponents(INetworkedComponentRegistry registry) : IComponentRegistry
    {
        public void RegisterLocalComponent<T>() where T : struct
        {
        }

        public void RegisterLocalComponent(Type component)
        {
        }

        public void RegisterComponent<T>(byte delivery = 0) where T : struct, INetworkedComponent
            => registry.RegisterComponent<T>(ModDelivery.Of(delivery));

        public void RegisterComponent(Type component, byte delivery)
            => registry.RegisterComponent(component, ModDelivery.Of(delivery));
    }

    public void Initialize(IServerDependencyContainer services, string modDirectory)
    {
        Services = services;
        ModDirectory = modDirectory;
        Init();
    }

    /// <summary>
    /// Reads <paramref name="fileName"/> from this mod's server folder and registers the result as a singleton.
    /// </summary>
    /// <remarks>
    /// A missing file yields defaults. A file that exists but does not parse, or that carries a key the
    /// config type does not declare, throws <see cref="ModConfigException" />.
    /// </remarks>
    protected void RegisterConfig<TConfig>(string fileName = ModConfigReader.DefaultFileName)
        where TConfig : class, new()
    {
        var config = ModConfigReader.Read<TConfig>(ModDirectory, fileName, Services.Resolve<ILogger>());
        Services.RegisterSingleton(config);
    }
    
    private class FunctionalArchetypeRegistration(Action<IArchetypeRegistry> callback) : IArchetypeRegistration
    {
        public void Register(IArchetypeRegistry registry)
        {
            callback(registry);
        }
    }
    
    /// <summary>
    /// Register new archetypes or modify existing.
    /// </summary>
    /// <param name="configure">The configuration callback.</param>
    [Obsolete("Part of SDK v0. Use the [Archetype] attribute instead.")]
    protected void RegisterArchetypes(Action<IArchetypeRegistry> configure)
    {
        Services.RegisterSingleton<IArchetypeRegistration>(new FunctionalArchetypeRegistration(configure));
    }

    /// <summary>
    /// Any components defined in the mod must be registered here.
    /// </summary>
    [Obsolete("Part of SDK v0. Use the [ArchetypeMixin] attribute instead.")]
    protected virtual void RegisterComponents(IComponentRegistry registry) { }

    protected abstract void Init();
}