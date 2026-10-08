using ReadyM.SDK.Archetypes;
using IComponentRegistry = ReadyM.Relay.Server.Sdk.Ecs.Components.IComponentRegistry;
using HostComponents = ReadyM.Relay.Server.Sdk.Ecs.Components.ModComponentRegistry;

namespace ReadyM.SDK.Server;

/// Tells the server what a mod's shapes add to the archetypes the game registers.
public static class ServerArchetypes
{
    /// <summary>Whether the replication pass owns this component rather than this one.</summary>
    private static bool Replicates(Type component)
        => typeof(ReadyM.Api.Multiplayer.ECS.Components.INetworkedComponent).IsAssignableFrom(component);

    /// Returns how many components were handed over, which a mod can log to see its shapes arrived.
    public static int ApplyExtensions(IComponentRegistry registry)
    {
        if (registry is not HostComponents host)
            return 0;

        var applied = 0;

        var registered = new HashSet<Type>();

        foreach (var shape in ArchetypeContributions.Shapes())
        {
            var contributed = new List<Type>();

            // What the shape itself brought.
            foreach (var component in ArchetypeContributions.OwnGenerated(shape))
            {
                if (registered.Add(component) && !Replicates(component))
                    host.RegisterLocalComponent(component);

                contributed.Add(component);
            }

            // What mods added with [Extends], again only what they generated. A mixin sitting on
            // a component the game already declares is the old pipeline's to place, not ours.
            contributed.AddRange(ArchetypeRegistry.GeneratedAddedTo(shape));

            if (contributed.Count == 0)
                continue;

            host.AddArchetypeExtensions(shape, contributed);
            applied += contributed.Count;
        }

        return applied;
    }
}
