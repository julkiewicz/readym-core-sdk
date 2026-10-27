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
    /// <summary>Whether <paramref name="origin"/> was allowed to raise an event about <paramref name="subject"/>.</summary>
    /// <param name="subject">The entity the event is about, or null for an event that is about nobody.</param>
    public static bool Allows(
        ServerNetworkedEventDispatchBase dispatch, Policy policy, EntityHandle? subject, PlayerId origin)
    {
        // The server may always raise, which is what lets it act for a bot without the policy refusing it
        if (origin == PlayerId.Server)
            return true;

        return policy switch
        {
            Policy.Anyone => true,
            Policy.ServerAuthoritative => false,
            Policy.SubjectOwner => Needs(subject).TryGetOwner(out var owner) && owner == origin,
            Policy.CellMaster => Simulated<Cell>(dispatch, subject, out var cell) && cell.MasterClient == origin,
            Policy.AreaMaster => Simulated<Area>(dispatch, subject, out var area) && area.MasterClient == origin,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unhandled network event policy."),
        };
    }

    /// <summary>The scope the subject is in, for the two policies that ask who simulates it.</summary>
    private static bool Simulated<TScope>(
        ServerNetworkedEventDispatchBase dispatch, EntityHandle? subject, out TScope scope)
        where TScope : struct, IArchetypeQueryable, IScope
        => ServerNetworkedEventScopes.TryScope(dispatch, Needs(subject), out scope);

    /// <summary>
    /// The subject a policy that checks one must have been handed. The generator refuses that pairing
    /// (NEVT004), so reaching here means a declaration and this check have drifted apart, and letting it
    /// quietly refuse instead would hide that for as long as nobody raised the event.
    /// </summary>
    private static EntityHandle Needs(EntityHandle? subject)
        => subject ?? throw new ArgumentOutOfRangeException(
            nameof(subject), "This policy checks a subject, and this event has none.");
}
