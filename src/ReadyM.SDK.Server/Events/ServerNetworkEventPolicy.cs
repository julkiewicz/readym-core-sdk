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
/// The authority check generated dispatch runs before any handler. It lives on the server because that is where
/// ownership and master-client state are, and because a client's own answer could not be trusted anyway. A client
/// that raises what its policy forbids has the event dropped here, which is where spoofing stops.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServerNetworkEventPolicy
{
    /// <summary>Whether <paramref name="origin"/> was allowed to raise an event about <paramref name="subject"/>.</summary>
    public static bool Allows<TShape>(
        ServerNetworkEventDispatchBase dispatch, Policy policy, TShape subject, PlayerId origin)
        where TShape : struct, IArchetypeQueryable
    {
        // The server may always raise, which is what lets it act for a bot without the policy refusing it
        if (origin == PlayerId.Server)
            return true;

        return policy switch
        {
            Policy.Anyone => true,
            Policy.SubjectOwner => EntityHandle.Of(subject).TryGetOwner(out var owner) && owner == origin,
            Policy.CellMaster => ServerNetworkEventScopes.TryScope<TShape, Cell>(dispatch, subject, out var cell)
                                 && cell.MasterClient == origin,
            Policy.AreaMaster => ServerNetworkEventScopes.TryScope<TShape, Area>(dispatch, subject, out var area)
                                 && area.MasterClient == origin,
            Policy.ServerAuthoritative => false,
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unhandled network event policy."),
        };
    }

    /// <summary>Whether an event with no subject may be raised by <paramref name="origin"/>.</summary>
    /// <remarks>
    /// Only the two policies that read nothing but the origin can get here: the generator refuses an event whose
    /// policy checks a subject and has none (NEVT004), so any other value means the two have drifted apart.
    /// </remarks>
    public static bool Allows(ServerNetworkEventDispatchBase dispatch, Policy policy, PlayerId origin)
        => policy switch
        {
            Policy.Anyone => true,
            Policy.ServerAuthoritative => origin == PlayerId.Server,
            _ => throw new ArgumentOutOfRangeException(
                nameof(policy), policy, "This policy checks a subject, and this event has none."),
        };
}
