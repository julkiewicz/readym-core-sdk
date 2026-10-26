using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Server.Events;

/// <exclude />
/// <summary>
/// How the server puts an accepted event on the wire: to the players its scope named, and a refusal to the origin
/// alone. A client never sends to a peer, so there is no relayed path here to get wrong.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServerNetworkEventTransport
{
    /// <summary>Sends an accepted event to the players its scope names.</summary>
    public static void FanOut(ServerNetworkEventDispatchBase dispatch, EventAudience audience, NetDataWriter writer)
        => throw new NotImplementedException();

    /// <summary>Tells the origin alone that its event was refused, and nobody else.</summary>
    public static void Reject(ServerNetworkEventDispatchBase dispatch, PlayerId origin, NetDataWriter writer)
        => throw new NotImplementedException();
}
