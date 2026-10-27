using System;
using System.Collections.Generic;
using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Protocol;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Server.Events;

/// <summary>
/// The server's half of one event: the policy, then every mod's handlers a stage at a time, then one fan-out
/// to the audience the scope names, or one refusal back to the origin.
/// </summary>
/// <remarks>
/// There is one per event, not one per mod. That is what makes a second mod's Validate able to refuse what the
/// event's own mod would have applied, and what keeps the event going out once however many mods handle it.
/// </remarks>
internal sealed class ServerNetworkedEventCoordinator<T>(
    INetworkedEventContract<T> contract, IEnumerable<INetworkedEventHandlers<T>> mods)
    : ServerNetworkedEventDispatchBase, INetworkedEventDispatch<T>
    where T : struct, INetworkedEvent
{
    /// <summary>What a policy refusal names as the thing that stopped it, since no mod's handler did.</summary>
    private const string ThePolicy = "policy";

    private readonly IReadOnlyList<INetworkedEventHandlers<T>> _mods = NetworkedEventStages.InOrder(mods);

    protected override RelayMessageCode Code() => contract.Code;

    protected override void Listen() => Hear(Code(), Arrived);

    /// <summary>The server raising it for itself, or for a player it is acting on behalf of.</summary>
    public void Raise(T networkEvent, PlayerId origin, bool vouched)
        => Dispatch(networkEvent, origin, raisedHere: true, vouched);

    /// <summary>Takes this event off the wire. The sender is the origin; the relay vouches for that much.</summary>
    private void Arrived(ServerEventHeader header, NetDataReader reader)
    {
        if (contract.TryRead(this, reader, out var e))
            Dispatch(e, header.Sender, raisedHere: false, vouched: false);
    }

    internal void Dispatch(T networkEvent, PlayerId origin, bool raisedHere, bool vouched)
    {
        var cancellation = new EventCancellation();
        var context = new EventContext(origin, raisedHere, cancellation);

        if (!vouched && !ServerNetworkedEventPolicy.Allows(this, contract.Policy, contract.Subject(in networkEvent), origin))
        {
            cancellation.Entering(ThePolicy);
            cancellation.Cancel(null);
            Refuse(ref networkEvent, context, cancellation);

            return;
        }

        NetworkedEventStages.Run(_mods, Stage.Validate, ref networkEvent, context);
        NetworkedEventStages.Run(_mods, Stage.Filter, ref networkEvent, context);

        if (cancellation.IsCancelled)
        {
            Refuse(ref networkEvent, context, cancellation);

            return;
        }

        NetworkedEventStages.Run(_mods, Stage.Apply, ref networkEvent, context);
        NetworkedEventStages.Run(_mods, Stage.Observe, ref networkEvent, context);

        // A server-scoped event was for the server, so there is nobody downstream to tell
        if (contract.Scope != EventScope.Server && TryAudience(in networkEvent, out var audience))
            ServerNetworkedEventTransport.FanOut(this, Code(), audience, origin, Written(in networkEvent));
    }

    private void Refuse(ref T networkEvent, EventContext context, EventCancellation cancellation)
    {
        ServerNetworkedEventTransport.Reject(this, Code(), context.Origin, Refused(in networkEvent, cancellation));

        NetworkedEventStages.Run(_mods, Stage.Rejected, ref networkEvent, context);
    }

    /// <summary>
    /// Who hears it. A subject in no cell or area leaves the event with no audience, which drops it rather
    /// than throwing: one bad event must not take the server down, and the origin has already been answered.
    /// </summary>
    private bool TryAudience(in T networkEvent, out EventAudience audience)
    {
        switch (contract.Scope)
        {
            case EventScope.Global:
                audience = EventAudience.Global;

                return true;

            case EventScope.Single:
                audience = EventAudience.Single(contract.Target(in networkEvent));

                return true;

            case EventScope.Area when contract.Subject(in networkEvent) is { } inArea:
                var foundArea = ServerNetworkedEventScopes.TryAreaOf(this, inArea, out var area);
                audience = EventAudience.InArea(area);

                return foundArea;

            case EventScope.Cell when contract.Subject(in networkEvent) is { } inCell:
                var foundCell = ServerNetworkedEventScopes.TryCellOf(this, inCell, out var cell);
                audience = EventAudience.InCell(cell);

                return foundCell;

            case EventScope.Area:
            case EventScope.Cell:
                audience = default;

                return false;

            case EventScope.Server:
                audience = EventAudience.Server;

                return true;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(networkEvent), contract.Scope, "Unhandled network event scope.");
        }
    }

    private static NetDataWriter Written(in T networkEvent)
    {
        var writer = new NetDataWriter();
        networkEvent.Serialize(writer);

        return writer;
    }

    /// <summary>What a refusal carries back: the event as it stood, who stopped it, and the reason if any.</summary>
    private NetDataWriter Refused(in T networkEvent, EventCancellation cancellation)
    {
        var writer = Written(in networkEvent);
        writer.Put(cancellation.StoppedBy);
        contract.WriteReason(writer, cancellation.Reason);

        return writer;
    }
}
