using LiteNetLib;
using LiteNetLib.Utils;
using ReadyM.Api.Multiplayer.Client;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

internal static class NetworkedEventTransport
{
    /// <summary>Sends a raised event to the server.</summary>
    public static void SendToServer(NetworkedEventDispatchBase dispatch, RelayMessageCode code, NetDataWriter payload)
    {
        if (dispatch.Relay is { } relay)
            SendToServer(relay, code, payload);
    }

    /// <summary>
    /// Sends a raised event to the server, for a mod that raises an event it declares no handler for.
    /// </summary>
    public static void SendToServer(IRpcClient relay, RelayMessageCode code, NetDataWriter payload)
    {
        var message = RelayMessage.ToServer(code, DeliveryMethod.ReliableOrdered);
        message.Writer.Put(payload.Data, 0, payload.Length);

        relay.SendMessage(message);
    }
}
