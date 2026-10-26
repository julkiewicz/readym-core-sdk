using LiteNetLib.Utils;
using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <summary>
/// What generated event code calls to put an event on the wire. A client sends only to the server; the server
/// sends an accepted event to the audience its scope named, and a refusal to the origin alone.
/// </summary>
public static class NetworkEventTransport
{
    /// <summary>Client: sends a raised event to the server, which is the only place it goes.</summary>
    public static void SendToServer(object sender, NetDataWriter writer) => throw new NotImplementedException();

    /// <summary>Server: sends an accepted event to the players its scope names.</summary>
    public static void FanOut(object sender, EventAudience audience, NetDataWriter writer)
        => throw new NotImplementedException();

    /// <summary>Server: tells the origin alone that its event was refused, and nobody else.</summary>
    public static void Reject(object sender, PlayerId origin, NetDataWriter writer)
        => throw new NotImplementedException();
}
