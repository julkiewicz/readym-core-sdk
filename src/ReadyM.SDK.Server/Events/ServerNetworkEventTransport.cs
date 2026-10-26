using System.ComponentModel;
using LiteNetLib;
using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;
using Audience = ReadyM.SDK.Attributes.Scope;
using EntityScope = ReadyM.SDK.Archetypes.Scope;

namespace ReadyM.SDK.Server.Events;

/// <exclude />
/// <summary>
/// How the server puts an accepted event on the wire: to the players its scope named, and a refusal to the origin
/// alone. A client never sends to a peer, so there is no relayed path here to get wrong.
/// </summary>
/// <remarks>A message is the event's code, then the frame, then the payload.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServerNetworkEventTransport
{
    /// <summary>Sends an accepted event to the players its scope names.</summary>
    public static void FanOut(
        ServerNetworkEventDispatchBase dispatch,
        RelayMessageCode code,
        EventAudience audience,
        PlayerId origin,
        NetDataWriter payload)
    {
        if (dispatch.Relay is not { } relay)
            return;

        var message = Framed(code, false, origin, payload);

        // Scope.Server stops here: the event was for the server, so there is nobody downstream to tell
        switch (audience.Kind)
        {
            case Audience.Single:
                relay.SendToOne(audience.Player, message, DeliveryMethod.ReliableOrdered);
                break;

            case Audience.Global:
                foreach (var player in Everyone(dispatch))
                    relay.SendToOne(player, message, DeliveryMethod.ReliableOrdered);
                break;

            case Audience.Area:
            case Audience.Cell:
                foreach (var player in Inside(dispatch, audience))
                    relay.SendToOne(player, message, DeliveryMethod.ReliableOrdered);
                break;
        }
    }

    /// <summary>Tells the origin alone that its event was refused, and nobody else.</summary>
    public static void Reject(
        ServerNetworkEventDispatchBase dispatch, RelayMessageCode code, PlayerId origin, NetDataWriter payload)
    {
        // A server-raised event has no connection to reply down, and nothing is waiting on one
        if (dispatch.Relay is not { } relay || origin == PlayerId.Server)
            return;

        relay.SendToOne(origin, Framed(code, true, origin, payload), DeliveryMethod.ReliableOrdered);
    }

    private static NetDataWriter Framed(RelayMessageCode code, bool refused, PlayerId origin, NetDataWriter payload)
    {
        var framed = NetworkEventFrame.Write(refused, origin, payload);

        var message = new NetDataWriter();
        message.Put((byte)code);
        message.Put(framed.Data, 0, framed.Length);

        return message;
    }

    /// <summary>Every connected player, for an event the whole session hears.</summary>
    private static List<PlayerId> Everyone(ServerNetworkEventDispatchBase dispatch)
    {
        var players = new List<PlayerId>();

        if (dispatch.Lookup is not { } entities)
            return players;

        foreach (var player in entities.Query<Player>())
            players.Add(player.PlayerId);

        return players;
    }

    /// <summary>
    /// The players in the audience's area or cell. A scope that no longer exists, because the last player in it
    /// left between the event being raised and it being accepted, leaves nobody to tell rather than throwing.
    /// </summary>
    /// <remarks>
    /// This walks the scope tree, which is where the entities an area holds are. A player is not one of them:
    /// a player entity is created in the server scope and its area is tracked by the relay host, which the SDK
    /// cannot read. Until the host exposes that membership, an area or cell audience resolves to nobody.
    /// </remarks>
    private static List<PlayerId> Inside(ServerNetworkEventDispatchBase dispatch, EventAudience audience)
    {
        var players = new List<PlayerId>();

        if (dispatch.Lookup is not { } entities)
            return players;

        if (audience.Kind == Audience.Cell)
        {
            if (entities.TryLookup<Cell, FullCellId>(audience.Cell, out var cell))
                Collect(entities, EntityScope.Of(cell), players);
        }
        else if (entities.TryLookup<Area, AreaId>(audience.Area, out var area))
        {
            Collect(entities, EntityScope.Of(area), players);
        }

        return players;
    }

    private static void Collect(IEntities entities, EntityScope scope, List<PlayerId> players)
    {
        foreach (var player in entities.Query<Player>().InScope(scope))
            players.Add(player.PlayerId);
    }
}
