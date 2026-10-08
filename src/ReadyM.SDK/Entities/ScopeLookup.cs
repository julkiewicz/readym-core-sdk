using ReadyM.SDK.Archetypes;

namespace ReadyM.SDK.Entities;

/// <summary>
/// Finds the scope of a given shape holding an entity.
/// </summary>
internal static class ScopeLookup
{
    /// <summary>
    /// How far up the chain to look before giving up.
    /// Exists because nothing stops the links forming a cycle.
    /// </summary>
    private const int MaxDepth = 32;

    /// <summary>The nearest scope holding this entity that is the shape asked for.</summary>
    /// <remarks>
    /// Upwards, not just the immediate holder: an entity created in a cell is in that cell's area as
    /// well, and a caller asking for the area means the one it is in rather than none.
    /// </remarks>
    public static bool TryFind<TScope>(IEntityApi api, EntityHandle handle, out TScope scope)
        where TScope : struct, IArchetypeQueryable, IScope
    {
        var entity = handle.RawEntity;

        for (var step = 0; step < MaxDepth; step++)
        {
            if (!api.TryGetScope(entity, out var holder))
                break;

            if (new EntityHandle(holder, api).TryAs(out scope))
                return true;

            entity = holder;
        }

        scope = default;
        return false;
    }
}
