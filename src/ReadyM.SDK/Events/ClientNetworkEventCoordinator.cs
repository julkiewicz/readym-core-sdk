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
internal sealed class ClientNetworkEventCoordinator<T>(
    INetworkEventContract<T> contract, IEnumerable<INetworkEventHandlers<T>> mods)
    : NetworkEventDispatchBase
    where T : struct, INetworkEvent
{
    private readonly IReadOnlyList<INetworkEventHandlers<T>> _mods = NetworkEventStages.InOrder(mods);

    protected override RelayMessageCode Code() => contract.Code;

    protected override void Listen() => Hear(Code(), Arrived);

    private void Arrived(ServerEventHeader header, NetDataReader reader)
    {
        if (!NetworkEventFrame.TryRead(reader, out var frame) || !contract.TryRead(this, reader, out var e))
            return;

        if (frame.Refused)
        {
            Refused(ref e, reader, frame);
            return;
        }

        var context = new EventContext(frame.Origin, frame.Origin == Me, new EventCancellation());

        NetworkEventStages.Run(_mods, Stage.Apply, ref e, context);
        NetworkEventStages.Run(_mods, Stage.Observe, ref e, context);
    }

    private void Refused(ref T networkEvent, NetDataReader reader, NetworkEventHeader frame)
    {
        GuessFailed();

        var cancellation = new EventCancellation();
        cancellation.Entering(reader.GetString());
        cancellation.Cancel(contract.ReadReason(reader));

        var context = new EventContext(frame.Origin, frame.Origin == Me, cancellation);

        NetworkEventStages.Run(_mods, Stage.Rejected, ref networkEvent, context);
    }
}
