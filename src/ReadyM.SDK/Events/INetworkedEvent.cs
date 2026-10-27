using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

/// <summary>
/// What the generator adds to a struct marked <see cref="Attributes.NetworkedEventAttribute"/>. A mod never writes
/// it.
/// </summary>
/// <remarks>
/// It carries both halves of putting the event on the wire, so raising one needs nothing generated per event on
/// the raising side. A client that only raises an event declares no handler for it, and so has no dispatch of
/// its own to go through.
/// </remarks>
/// <exclude />
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkedEvent
{
    /// <summary>This event's code on the wire, once the host has placed its mod's block.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    RelayMessageCode Code { get; }

    /// <summary>Writes this event's members, in the order they are declared.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    void Serialize(NetDataWriter writer);
}
