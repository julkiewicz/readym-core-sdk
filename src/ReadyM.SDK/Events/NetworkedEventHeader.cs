using LiteNetLib.Utils;
using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <summary>
/// Precedes a networked event's payload on the wire. An event and a refusal of it arrive on the same code, and
/// this tells them apart.
/// </summary>
internal readonly struct NetworkedEventHeader(bool refused, PlayerId origin)
{
    private const byte AcceptedKind = 0;
    private const byte RefusedKind = 1;

    /// <summary>Whether this is a refusal of the event rather than the event.</summary>
    public bool Refused { get; } = refused;

    /// <summary>The player who raised the event, or <see cref="PlayerId.Server"/>.</summary>
    public PlayerId Origin { get; } = origin;

    public void Write(NetDataWriter writer)
    {
        writer.Put(Refused ? RefusedKind : AcceptedKind);
        writer.Put(Origin);
    }

    /// <summary>Reads the header, leaving the reader at the payload.</summary>
    /// <returns>False when the message is too short to hold a header.</returns>
    public static bool TryRead(NetDataReader reader, out NetworkedEventHeader header)
    {
        header = default;

        // The kind byte, plus at least one byte of the origin
        if (reader.AvailableBytes < 2)
            return false;

        var refused = reader.GetByte() == RefusedKind;
        var origin = reader.Get<PlayerId>();
        header = new NetworkedEventHeader(refused, origin);

        return true;
    }
}
