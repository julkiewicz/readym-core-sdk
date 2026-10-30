using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.SDK.Events;
using Xunit;

namespace ReadyM.SDK.Tests.Events;

/// <summary>What the server puts in front of an event it sends on, and what a client reads back.</summary>
public class NetworkedEventHeaderTests
{
    private static readonly PlayerId Raiser = new(4);

    private static NetDataReader Sent(bool refused, PlayerId origin, int payload)
    {
        var message = new NetDataWriter();
        new NetworkedEventHeader(refused, origin).Write(message);
        message.Put(payload);

        return new NetDataReader(message.Data, 0, message.Length);
    }

    [Fact]
    public void An_accepted_event_names_who_raised_it()
    {
        var reader = Sent(false, Raiser, 7);

        Assert.True(NetworkedEventHeader.TryRead(reader, out var header));
        Assert.False(header.Refused);
        Assert.Equal(Raiser, header.Origin);
        Assert.Equal(7, reader.GetInt());
    }

    [Fact]
    public void A_refusal_says_so()
    {
        var reader = Sent(true, Raiser, 7);

        Assert.True(NetworkedEventHeader.TryRead(reader, out var header));
        Assert.True(header.Refused);
        Assert.Equal(Raiser, header.Origin);
    }

    /// <summary>
    /// A server raising for a bot is the origin, and a client has to be able to read that rather than find a
    /// player id that belongs to nobody.
    /// </summary>
    [Fact]
    public void An_event_the_server_raised_names_the_server()
    {
        var reader = Sent(false, PlayerId.Server, 1);

        Assert.True(NetworkedEventHeader.TryRead(reader, out var header));
        Assert.Equal(PlayerId.Server, header.Origin);
    }

    /// <summary>
    /// A short message is a peer sending garbage, not our own broken state. Dropping it loudly is the handler's
    /// job; reading past the end would throw inside the relay's own callback.
    /// </summary>
    [Fact]
    public void A_message_too_short_to_hold_a_header_is_not_read()
    {
        var reader = new NetDataReader(new byte[1]);

        Assert.False(NetworkedEventHeader.TryRead(reader, out _));
    }
}
