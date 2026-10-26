using System.ComponentModel;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Server.Events;

/// <exclude />
/// <summary>
/// Where generated dispatch reads the audience of a scoped event from its subject, so a mod never names a cell or
/// an area itself. Server-side, because the scope an entity is in is the server's to know.
/// </summary>
/// <remarks>
/// Try-shaped on purpose: a subject that is in no cell leaves the event with no audience, and one bad event must
/// not take the server down. Dispatch drops it, the way a failed shape lookup skips an RPC handler.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServerNetworkEventScopes
{
    /// <summary>The cell the subject is in.</summary>
    public static bool TryCellOf<TShape>(ServerNetworkEventDispatchBase dispatch, TShape subject, out FullCellId cell)
        where TShape : struct, IArchetypeQueryable
    {
        if (TryScope<TShape, Cell>(dispatch, subject, out var found))
        {
            cell = found.FullCellId;
            return true;
        }

        cell = default;
        return false;
    }

    /// <summary>The area the subject is in.</summary>
    public static bool TryAreaOf<TShape>(ServerNetworkEventDispatchBase dispatch, TShape subject, out AreaId area)
        where TShape : struct, IArchetypeQueryable
    {
        if (TryScope<TShape, Area>(dispatch, subject, out var found))
        {
            area = found.AreaId;
            return true;
        }

        area = default;
        return false;
    }

    /// <summary>
    /// The nearest scope of a shape holding the subject. Upwards, not just the immediate holder, so an entity in
    /// a cell is in that cell's area too.
    /// </summary>
    internal static bool TryScope<TShape, TScope>(
        ServerNetworkEventDispatchBase dispatch, TShape subject, out TScope scope)
        where TShape : struct, IArchetypeQueryable
        where TScope : struct, IArchetypeQueryable, IScope
    {
        if (dispatch.Entities is { } entities)
            return ScopeLookup.TryFind(entities.Api, EntityHandle.Of(subject), out scope);

        scope = default;
        return false;
    }
}
