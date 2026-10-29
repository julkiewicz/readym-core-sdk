using LiteNetLib.Utils;
using ReadyM.Api.Multiplayer.Protocol;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <summary>
/// A client's half of a networked event.
/// Listens for the code, and runs every mod's Apply, and then Observe or Rejected.
/// </summary>
internal sealed class ClientNetworkedEventCoordinator<T>(
    INetworkedEventContract<T> contract,
    IEnumerable<INetworkedEventHandlers<T>> eventHandlers
) : NetworkedEventDispatchBase
    where T : struct, INetworkedEvent
{
    private readonly IReadOnlyList<INetworkedEventHandlers<T>> _eventHandlers 
        = NetworkedEventStages.InOrder(eventHandlers);

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

        var context = new EventContext(frame.Origin, new EventCancellation());

        NetworkedEventStages.Run(_eventHandlers, Stage.Apply, ref e, context);
        NetworkedEventStages.Run(_eventHandlers, Stage.Observe, ref e, context);
    }

    private void Refused(ref T networkEvent, NetDataReader reader, NetworkedEventHeader frame)
    {
        PredictionFailed();

        var cancellation = new EventCancellation();
        cancellation.SetCurrentMod(reader.GetString());
        cancellation.Cancel(contract.ReadReason(reader));

        var context = new EventContext(frame.Origin, cancellation);

        NetworkedEventStages.Run(_eventHandlers, Stage.Rejected, ref networkEvent, context);
    }
}