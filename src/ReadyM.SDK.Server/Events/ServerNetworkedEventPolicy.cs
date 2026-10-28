using System;
using System.ComponentModel;
using ReadyM.Api.Idents;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;

namespace ReadyM.SDK.Server.Events;

/// <exclude />
/// <summary>
/// The authority check the coordinator runs before any handler. It lives on the server because that is where
/// ownership and master-client state are, and because a client's own answer could not be trusted anyway. A
/// client that raises what its policy forbids has the event refused here, which is where spoofing stops.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServerNetworkedEventPolicy
{
    /// <summary>Whether <paramref name="origin"/> was allowed to raise the event.</summary>
    /// <param name="subject">The entity the event is about, or null for an event that is about nobody.</param>
    /// <param name="area">The area the event names with [Area], or null where it names none.</param>
    /// <param name="cell">The cell the event names with [Cell], or null where it names none.</param>
    /// <param name="origin">
    /// Who sent it, as the server knows from the connection it came in on. Never something the event says about
    /// itself, which is why a master policy compares the place's master with this and not with a member.
    /// </param>
    public static bool Allows(
        ServerNetworkedEventDispatchBase dispatch,
        Policy policy,
        EntityHandle? subject,
        AreaId? area,
        FullCellId? cell,
        PlayerId origin)
    {
        // The server may always raise, which is what lets it act for a bot without the policy refusing it
        if (origin == PlayerId.Server)
            return true;

        return policy switch
        {
            Policy.Anyone => true,
            Policy.ServerAuthoritative => false,
            Policy.SubjectOwner => Needs(subject).TryGetOwner(out var owner) && owner == origin,
            Policy.CellMaster => ServerNetworkedEventScopes.TryCell(dispatch, Needs(cell, "[Cell]"), out var named)
                && named.MasterClient == origin,
            Policy.AreaMaster => ServerNetworkedEventScopes.TryArea(dispatch, Needs(area, "[Area]"), out var named)
                && named.MasterClient == origin,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unhandled network event policy."),
        };
    }

    /// <summary>
    /// The subject a policy that checks one must have been handed. The generator refuses that pairing
    /// (NEVT004), so reaching here means a declaration and this check have drifted apart, and letting it
    /// quietly refuse instead would hide that for as long as nobody raised the event.
    /// </summary>
    private static EntityHandle Needs(EntityHandle? subject)
        => subject ?? throw new ArgumentOutOfRangeException(
            nameof(subject), "This policy checks a subject, and this event has none.");

    /// <summary>
    /// The place a master policy must have been handed. The generator refuses an event that names none (NEVT007,
    /// NEVT008), so reaching here means the declaration and this check have drifted apart.
    /// </summary>
    private static TId Needs<TId>(TId? place, string marker) where TId : struct
        => place ?? throw new ArgumentOutOfRangeException(
            nameof(place), $"This policy checks the place the event names with {marker}, and this event names none.");
}
