using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.SDK.Events;
using Xunit;

namespace ReadyM.SDK.Tests.Events;

/// <summary>
/// What the server puts in front of an event it sends on, and what a client reads back. Both arrive on the
/// event's own code, so the frame is the only thing that says which of the two this is and who raised it.
/// </summary>
public class NetworkEventFrameTests
{
    private static readonly PlayerId Raiser = new(4);

    private static NetDataReader Sent(bool refused, PlayerId origin, int payload)
    {
        var body = new NetDataWriter();
        body.Put(payload);

        var message = NetworkEventFrame.Write(refused, origin, body);

        // The code goes in front on the way out and is stripped before a handler sees the rest
        var reader = new NetDataReader(message.Data, 0, message.Length);

        return reader;
    }

    [Fact]
    public void An_accepted_event_names_who_raised_it()
    {
        var reader = Sent(false, Raiser, 7);

        Assert.True(NetworkEventFrame.TryRead(reader, out var frame));
        Assert.False(frame.Refused);
        Assert.Equal(Raiser, frame.Origin);
        Assert.Equal(7, reader.GetInt());
    }

    [Fact]
    public void A_refusal_says_so()
    {
        var reader = Sent(true, Raiser, 7);

        Assert.True(NetworkEventFrame.TryRead(reader, out var frame));
        Assert.True(frame.Refused);
        Assert.Equal(Raiser, frame.Origin);
    }

    /// <summary>
    /// A server raising for a bot is the origin, and a client has to be able to read that rather than find a
    /// player id that belongs to nobody.
    /// </summary>
    [Fact]
    public void An_event_the_server_raised_names_the_server()
    {
        var reader = Sent(false, PlayerId.Server, 1);

        Assert.True(NetworkEventFrame.TryRead(reader, out var frame));
        Assert.Equal(PlayerId.Server, frame.Origin);
    }

    /// <summary>
    /// A short message is a peer sending garbage, not our own broken state. Dropping it loudly is the handler's
    /// job; reading past the end would throw inside the relay's own callback.
    /// </summary>
    [Fact]
    public void A_message_too_short_to_hold_a_frame_is_refused_rather_than_read()
    {
        var reader = new NetDataReader(new byte[1]);

        Assert.False(NetworkEventFrame.TryRead(reader, out _));
    }
}
