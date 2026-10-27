using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// What the server puts in front of an event it sends on: whether this is the event or a refusal of it, and who
/// raised it. Both arrive on the event's own code, so a receiver cannot tell them apart without this.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkedEventFrame
{
    private const byte Accepted = 0;

    private const byte Refused = 1;

    /// <summary>Frame plus payload, ready for the code to go in front of it.</summary>
    public static NetDataWriter Write(bool refused, PlayerId origin, NetDataWriter payload)
    {
        var message = new NetDataWriter();
        message.Put(refused ? Refused : Accepted);
        origin.Serialize(message);
        message.Put(payload.Data, 0, payload.Length);

        return message;
    }

    /// <summary>Reads the frame, leaving the reader on the event itself.</summary>
    /// <returns>False when the message is too short to hold one, which is a peer sending garbage.</returns>
    public static bool TryRead(NetDataReader reader, out NetworkedEventHeader header)
    {
        header = default;

        // The kind byte plus at least one of the origin's, which is a VarUShort and so has no fixed size.
        // A message truncated further in surfaces as a throw the relay logs, rather than a silent drop.
        if (reader.AvailableBytes < 2)
            return false;

        var refused = reader.GetByte() == Refused;
        var origin = new PlayerId();
        origin.Deserialize(reader);

        header = new NetworkedEventHeader(refused, origin);
        return true;
    }
}

/// <exclude />
/// <summary>What a frame said.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct NetworkedEventHeader(bool refused, PlayerId origin)
{
    /// <summary>Whether this is a refusal of the event rather than the event.</summary>
    public bool Refused { get; } = refused;

    /// <summary>The player whose event this is, or <c>PlayerId.Server</c>.</summary>
    public PlayerId Origin { get; } = origin;
}
