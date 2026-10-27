using System.Collections.Generic;
using LiteNetLib.Utils;
using ReadyM.Api.Multiplayer.Protocol;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <summary>
/// A client's half of one event: it listens for the code, and runs every mod's Apply and then every mod's
/// Observe over what the server accepted, or every mod's Rejected over what it refused.
/// </summary>
/// <remarks>
/// There is one per event the process knows about, declared by the Common project both sides share, not one
/// per mod. A client that only raises an event still gets this one, which is how a refusal of its own event
/// finds the guess it has to take back.
/// </remarks>
internal sealed class ClientNetworkedEventCoordinator<T>(
    INetworkedEventContract<T> contract, IEnumerable<INetworkedEventHandlers<T>> mods)
    : NetworkedEventDispatchBase
    where T : struct, INetworkedEvent
{
    private readonly IReadOnlyList<INetworkedEventHandlers<T>> _mods = NetworkedEventStages.InOrder(mods);

    protected override RelayMessageCode Code() => contract.Code;

    protected override void Listen() => Hear(Code(), Arrived);

    private void Arrived(ServerEventHeader header, NetDataReader reader)
    {
        if (!NetworkedEventFrame.TryRead(reader, out var frame) || !contract.TryRead(this, reader, out var e))
            return;

        if (frame.Refused)
        {
            Refused(ref e, reader, frame);
            return;
        }

        var context = new EventContext(frame.Origin, frame.Origin == Me, new EventCancellation());

        NetworkedEventStages.Run(_mods, Stage.Apply, ref e, context);
        NetworkedEventStages.Run(_mods, Stage.Observe, ref e, context);
    }

    private void Refused(ref T networkEvent, NetDataReader reader, NetworkedEventHeader frame)
    {
        GuessFailed();

        var cancellation = new EventCancellation();
        cancellation.Entering(reader.GetString());
        cancellation.Cancel(contract.ReadReason(reader));

        var context = new EventContext(frame.Origin, frame.Origin == Me, cancellation);

        NetworkedEventStages.Run(_mods, Stage.Rejected, ref networkEvent, context);
    }
}
