using System.ComponentModel;
using LiteNetLib.Utils;

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
    public static void SendToServer(NetworkEventDispatchBase dispatch, NetDataWriter writer) => throw new NotImplementedException();
}
