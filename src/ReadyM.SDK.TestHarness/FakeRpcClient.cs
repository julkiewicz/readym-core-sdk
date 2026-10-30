using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Client;
using ReadyM.Api.Multiplayer.Protocol;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.TestHarness;

/// <summary>Stands in for a client's connection to the relay: counts what is sent, and delivers server messages.</summary>
internal sealed class FakeRpcClient : IRpcClient
{
    private readonly Dictionary<RelayMessageCode, Action<ServerEventHeader, NetDataReader>> _serverHandlers = [];

    public PlayerId? PlayerId { get; set; } = new(1);

    public int Sent { get; private set; }

    public int Subscriptions => _serverHandlers.Count;

    public void Deliver(RelayMessageCode code, NetDataWriter body)
        => _serverHandlers[code](new ServerEventHeader(code, default), new NetDataReader(body.Data, 0, body.Length));

    public void SendMessage(RelayMessage message) => Sent++;

    public void AddServerRpcMessageHandler(RelayMessageCode code, Action<ServerEventHeader, NetDataReader> handler)
        => _serverHandlers[code] = handler;

    public void RemoveServerRpcMessageHandler(RelayMessageCode code, Action<ServerEventHeader, NetDataReader> handler)
        => _serverHandlers.Remove(code);

    public void AddClientRpcMessageHandler(RelayMessageCode code, Action<CustomRelayEventHeader, NetDataReader> handler)
        => throw new NotSupportedException();

    public void AddClientRpcMessageHandler(
        RelayMessageCode min, RelayMessageCode max, Action<CustomRelayEventHeader, NetDataReader> handler)
        => throw new NotSupportedException();

    public void RemoveClientRpcMessageHandler(RelayMessageCode code, Action<CustomRelayEventHeader, NetDataReader> handler)
        => throw new NotSupportedException();

    public void RemoveClientRpcMessageHandler(
        RelayMessageCode min, RelayMessageCode max, Action<CustomRelayEventHeader, NetDataReader> handler)
        => throw new NotSupportedException();
}
