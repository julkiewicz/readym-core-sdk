using ReadyM.SDK.Archetypes;
using ReadyM.Relay.Server.Sdk.Ecs.Components;

namespace ReadyM.SDK.Server;

/// <summary>Tells the server what a mod's shapes add to the archetypes the game registers.</summary>
internal static class ServerArchetypes
{
    /// <summary>Whether the replication pass owns this component rather than this one.</summary>
    private static bool Replicates(Type component)
        => typeof(ReadyM.Api.Multiplayer.ECS.Components.INetworkedComponent).IsAssignableFrom(component);

    /// <summary>Returns how many components were handed over, which a mod can log to see its shapes arrived.</summary>
    public static int ApplyExtensions(ModComponentRegistryFixMe registry)
    {
        var applied = 0;

        foreach (var shape in ArchetypeContributions.Shapes())
        {
            var contributed = Contributed(shape);

            if (contributed.Count == 0)
                continue;

            registry.AddArchetypeExtensions(shape, contributed);
            applied += contributed.Count;
        }

        return applied;
    }

    /// <summary>Every component a mod adds to the game's archetype for a shape.</summary>
    internal static List<Type> Contributed(Type shape)
    {
        var contributed = new List<Type>(ArchetypeContributions.OwnGenerated(shape));
        
        contributed.AddRange(ArchetypeRegistry.GeneratedAddedTo(shape));
        
        foreach (var change in contributed.Where(Replicates).Select(ChangeComponents.Of).OfType<Type>().ToList())
        {
            if (!contributed.Contains(change))
                contributed.Add(change);
        }

        return contributed;
    }
}
