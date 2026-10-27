using System.ComponentModel;
using LiteNetLib;
using LiteNetLib.Utils;
using ReadyM.Api.Multiplayer.Client;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// How a client puts a raised event on the wire. The server is the only place it goes: the server decides who
/// else receives it, which is what lets it validate, change or stop the event before anyone hears about it.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkEventTransport
{
    /// <summary>Sends a raised event to the server.</summary>
    /// <remarks>
    /// The code travels in the message header rather than in the payload, which is how a client-to-server RPC
    /// carries it. The server's way back is the other shape, a code byte written into the data, because that is
    /// what the two sides of the relay already do.
    /// </remarks>
    public static void SendToServer(
        NetworkEventDispatchBase dispatch, RelayMessageCode code, NetDataWriter payload)
    {
        if (dispatch.Relay is { } relay)
            SendToServer(relay, code, payload);
    }

    /// <summary>
    /// The same send, for a mod that raises an event it declares no handler for. It has no dispatch of its own,
    /// since nothing was generated for it, and needs none: a client has one destination.
    /// </summary>
    public static void SendToServer(IRpcClient relay, RelayMessageCode code, NetDataWriter payload)
    {
        var message = RelayMessage.ToServer(code, DeliveryMethod.ReliableOrdered);
        message.Writer.Put(payload.Data, 0, payload.Length);

        relay.SendMessage(message);
    }
}
