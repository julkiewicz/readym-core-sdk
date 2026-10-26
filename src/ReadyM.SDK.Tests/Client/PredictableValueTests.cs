using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Events;
using ReadyM.SDK.Tests.Client.Fixtures;
using Xunit;

namespace ReadyM.SDK.Tests.Client;

/// <summary>
/// A [Predictable] value through the accessors the generator actually emitted, rather than through a stand-in
/// for them. What a mod sees is one property: it reads the guess while one stands and the server's value once
/// it does not, and never learns which it got.
/// </summary>
public class PredictableValueTests : ClientSdkTest
{
    private const RelayMessageCode Code = RelayMessageCode.MaxServerRpcEvent;

    private readonly NetworkEventPredictions _predictions = new();

    private Settlement Town() => Entities.Create<Settlement>();

    [Fact]
    public void A_value_nobody_has_guessed_at_reads_as_the_server_left_it()
    {
        var town = Town();

        town.LockdownActive = true;

        Assert.True(town.LockdownActive);
    }

    [Fact]
    public void A_guess_is_what_the_value_reads_as_while_it_stands()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.LockdownActive = true);

        Assert.True(town.LockdownActive);
    }

    /// <summary>
    /// The guess is held beside the entity, never written into the replicated component. That is the whole of
    /// why a client is allowed to write a value the server owns here: none of it leaves the machine.
    /// </summary>
    [Fact]
    public void A_guess_never_reaches_the_value_itself()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.LockdownActive = true);
        _predictions.Refused(Code);

        Assert.False(town.LockdownActive);
    }

    /// <summary>
    /// Dropping rather than restoring, read through the generated getter: the server accepting the event is an
    /// ordinary change that lands while the guess still stands, and the read has to end up on it.
    /// </summary>
    [Fact]
    public void The_server_answering_while_the_guess_stands_is_what_the_read_falls_through_to()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.LockdownActive = true);
        town.LockdownActive = true;
        _predictions.Refused(Code);

        Assert.True(town.LockdownActive);
    }

    [Fact]
    public void A_guess_nothing_answered_is_taken_back_when_it_runs_out()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.LockdownActive = true);
        _predictions.Expire(NetworkEventPredictions.Ttl);

        Assert.False(town.LockdownActive);
    }

    /// <summary>One entity's guess is not another's, which is what keying the guess by entity is for.</summary>
    [Fact]
    public void A_guess_about_one_entity_leaves_the_others_alone()
    {
        var guessed = Town();
        var other = Town();

        _predictions.Open(Code, 0).Predicting(() => guessed.LockdownActive = true);

        Assert.True(guessed.LockdownActive);
        Assert.False(other.LockdownActive);
    }

    /// <summary>
    /// Only the marked value is guessed at. Writing its neighbour inside the same callback goes through to the
    /// component as it always does, so it is still there once the guess has been taken back.
    /// </summary>
    [Fact]
    public void A_value_that_is_not_marked_is_written_rather_than_guessed()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() =>
        {
            town.LockdownActive = true;
            town.LockdownHour = 22;
        });

        _predictions.Refused(Code);

        Assert.False(town.LockdownActive);
        Assert.Equal(22, town.LockdownHour);
    }

    /// <summary>
    /// The shape's own property and the archetype's projection of it read one guess. Two stores, or a guess
    /// held on the projection alone, would leave the same value reading two ways on one entity.
    /// </summary>
    [Fact]
    public void The_shape_and_the_archetype_read_one_guess()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.LockdownActive = true);

        Assert.True(ReadyM.SDK.Entities.EntityHandle.Of(town).As<Lockdown>().Active);
        Assert.True(town.LockdownActive);
    }

    /// <summary>
    /// A write outside a guess is the server writing its own value, and has to reach the component. The guess
    /// path is reached only from inside Predicting, so nothing else is quietly held back.
    /// </summary>
    [Fact]
    public void A_write_outside_a_guess_still_reaches_the_value()
    {
        var town = Town();

        town.LockdownActive = true;
        _predictions.Expire(NetworkEventPredictions.Ttl * 10);

        Assert.True(town.LockdownActive);
    }
}
