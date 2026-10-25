using ReadyM.SDK.Archetypes.Core;

namespace ReadyM.SDK.Archetypes;

// FixMe(2026-10-18-twins-m4): reads the static ArchetypeRegistry. It goes with it.
internal static class ArchetypeContributions
{
    /// <summary>
    /// The shapes the SDK declares for every game. They contribute even when nothing
    /// extended them. An archetype with no values of its own still has a marker, and without it a
    /// query for the shape finds nothing.
    /// </summary>
    private static readonly Type[] Core = [typeof(World), typeof(Area), typeof(Player), typeof(Cell)];

    public static IEnumerable<Type> Shapes()
        => Core.Concat(ArchetypeRegistry.Declared()).Concat(ArchetypeRegistry.Extended()).Distinct();

    /// <summary>The components the SDK generated for a shape.</summary>
    public static IEnumerable<Type> OwnGenerated(Type shape)
    {
        if (Activator.CreateInstance(shape) is not IArchetypeQueryable queryable)
            yield break;

        foreach (var component in queryable.Components.Types)
            if (component.Assembly == shape.Assembly)
                yield return component;
    }
}