using System.ComponentModel;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Server.Events;

/// <exclude />
/// <summary>
/// Where the coordinator reads the audience of a scoped event from its subject, so a mod never names a cell or
/// an area itself. Server-side, because the scope an entity is in is the server's to know.
/// </summary>
/// <remarks>
/// Try-shaped on purpose: a subject that is in no cell leaves the event with no audience, and one bad event
/// must not take the server down. The coordinator drops it, the way a failed shape lookup skips an RPC handler.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServerNetworkEventScopes
{
    /// <summary>The cell the subject is in.</summary>
    public static bool TryCellOf(ServerNetworkEventDispatchBase dispatch, EntityHandle subject, out FullCellId cell)
    {
        if (TryScope<Cell>(dispatch, subject, out var found))
        {
            cell = found.FullCellId;

            return true;
        }

        cell = default;

        return false;
    }

    /// <summary>The area the subject is in.</summary>
    public static bool TryAreaOf(ServerNetworkEventDispatchBase dispatch, EntityHandle subject, out AreaId area)
    {
        if (TryScope<Area>(dispatch, subject, out var found))
        {
            area = found.AreaId;

            return true;
        }

        area = default;

        return false;
    }

    /// <summary>
    /// The nearest scope of a shape holding the subject. Upwards, not just the immediate holder, so an entity
    /// in a cell is in that cell's area too.
    /// </summary>
    internal static bool TryScope<TScope>(
        ServerNetworkEventDispatchBase dispatch, EntityHandle subject, out TScope scope)
        where TScope : struct, IArchetypeQueryable, IScope
    {
        if (dispatch.Entities is { } entities)
            return ScopeLookup.TryFind(entities.Api, subject, out scope);

        scope = default;

        return false;
    }
}
