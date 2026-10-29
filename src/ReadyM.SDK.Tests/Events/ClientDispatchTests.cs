using System;
using System.Collections.Generic;
using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Client;
using ReadyM.Api.Multiplayer.ECS.Managers;
using ReadyM.Api.Multiplayer.Protocol;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Events;
using Xunit;

namespace ReadyM.SDK.Tests.Events;

/// <summary>
/// What a client dispatch does with a message on its event's code, which is the half generated code leans on:
/// it subscribes once, knows which machine it is, and stops listening when it goes.
/// </summary>
public class ClientDispatchTests
{
    private const RelayMessageCode Code = RelayMessageCode.MaxServerRpcEvent;

    private static readonly PlayerId Me = new(2);

    private static readonly PlayerId Somebody = new(5);

    /// <summary>Stands in for the generated dispatch, which is what every real one is.</summary>
    private sealed class Dispatch : NetworkedEventDispatchBase
    {
        public List<(bool Refused, PlayerId Origin)> Arrivals { get; } = [];

        protected override RelayMessageCode Code() => ClientDispatchTests.Code;

        protected override void Listen() => Hear(Code(), Arrived);

        private void Arrived(ServerEventHeader header, NetDataReader reader)
        {
            if (NetworkedEventFrame.TryRead(reader, out var frame))
                Arrivals.Add((frame.Refused, frame.Origin));
        }
    }

    private sealed class Relay : IRpcClient
    {
        private readonly Dictionary<RelayMessageCode, Action<ServerEventHeader, NetDataReader>> _server = new();

        public PlayerId? PlayerId { get; set; } = ClientDispatchTests.Me;

        public int Subscriptions => _server.Count;

        public void Deliver(RelayMessageCode code, NetDataWriter body)
        {
            if (_server.TryGetValue(code, out var handler))
                handler(new ServerEventHeader(code, default), new NetDataReader(body.Data, 0, body.Length));
        }

        public void AddServerRpcMessageHandler(RelayMessageCode code, Action<ServerEventHeader, NetDataReader> h)
            => _server[code] = h;

        public void RemoveServerRpcMessageHandler(RelayMessageCode code, Action<ServerEventHeader, NetDataReader> h)
            => _server.Remove(code);

        public void AddClientRpcMessageHandler(RelayMessageCode code, Action<CustomRelayEventHeader, NetDataReader> h)
            => throw new NotSupportedException();

        public void AddClientRpcMessageHandler(
            RelayMessageCode min, RelayMessageCode max, Action<CustomRelayEventHeader, NetDataReader> h)
            => throw new NotSupportedException();

        public void RemoveClientRpcMessageHandler(RelayMessageCode code, Action<CustomRelayEventHeader, NetDataReader> h)
            => throw new NotSupportedException();

        public void RemoveClientRpcMessageHandler(
            RelayMessageCode min, RelayMessageCode max, Action<CustomRelayEventHeader, NetDataReader> h)
            => throw new NotSupportedException();

        public void SendMessage(RelayMessage message)
        {
        }
    }

    private static (Dispatch Dispatch, Relay Relay) Listening()
    {
        var relay = new Relay();
        var dispatch = new Dispatch { Relay = relay };
        dispatch.OnScopeStart();

        return (dispatch, relay);
    }

    [Fact]
    public void An_event_from_somebody_else_is_not_one_this_machine_raised()
    {
        var (dispatch, relay) = Listening();

        relay.Deliver(Code, NetworkedEventFrame.Write(false, Somebody, new NetDataWriter()));

        Assert.Equal([(false, Somebody)], dispatch.Arrivals);
    }

    [Fact]
    public void A_refusal_arrives_on_the_same_code_marked_as_one()
    {
        var (dispatch, relay) = Listening();

        relay.Deliver(Code, NetworkedEventFrame.Write(true, Me, new NetDataWriter()));

        Assert.Equal([(true, Me)], dispatch.Arrivals);
    }

    /// <summary>A dispatch that goes while the session lives would otherwise keep answering for a dead mod.</summary>
    [Fact]
    public void A_disposed_dispatch_stops_listening()
    {
        var (dispatch, relay) = Listening();

        Assert.Equal(1, relay.Subscriptions);

        dispatch.Dispose();

        Assert.Equal(0, relay.Subscriptions);
    }

    /// <summary>
    /// A client that has not been told its own id yet cannot claim an event as its own, or every arrival would
    /// look locally raised and every SkipOnRaiser Apply would be skipped.
    /// </summary>
    [Fact]
    public void A_client_with_no_id_yet_claims_nothing_as_its_own()
    {
        var relay = new Relay { PlayerId = null };
        var dispatch = new Dispatch { Relay = relay };
        dispatch.OnScopeStart();

        relay.Deliver(Code, NetworkedEventFrame.Write(false, Somebody, new NetDataWriter()));

        Assert.Equal([(false, Somebody)], dispatch.Arrivals);
    }

    /// <summary>Generated code calls this on a host that may not have wired it, so it must not fault.</summary>
    [Fact]
    public void An_unwired_dispatch_listens_for_nothing_rather_than_throwing()
    {
        var dispatch = new Dispatch();

        dispatch.OnScopeStart();
        dispatch.Dispose();

        Assert.Empty(dispatch.Arrivals);
    }
}
