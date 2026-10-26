using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Client;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Client.Events;

/// <summary>
/// Raising an event from client code. Nothing runs here: the event goes to the server, which decides whether it
/// happened and tells this client back. That is the whole point of the server sitting in the middle.
/// </summary>
/// <remarks>
/// It needs nothing generated per event, because the event carries its own code and writes its own members. A
/// mod that raises an event it declares no handler for has no dispatch, and this is why it does not need one.
/// </remarks>
internal sealed class ClientNetworkEvents(IRpcClient relay) : INetworkEvents
{
    public void Raise<T>(T networkEvent) where T : struct, INetworkEvent
    {
        var payload = new NetDataWriter();
        networkEvent.Serialize(payload);

        NetworkEventTransport.SendToServer(relay, networkEvent.Code, payload);
    }

    public void RaiseAs<T>(PlayerId origin, T networkEvent) where T : struct, INetworkEvent
        => throw new InvalidOperationException(
            $"Only the server may raise an event as another player. {typeof(T).Name} was raised as {origin} on a client.");
}
