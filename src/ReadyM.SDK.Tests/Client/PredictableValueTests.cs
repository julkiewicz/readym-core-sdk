using ReadyM.Api.Multiplayer.ECS.Jobs;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Entities;
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

    private readonly NetworkedEventPredictions _predictions = new();

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

        _predictions.Open(Code, 0).Predicting(() => town.PredictLockdownActive(true));

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

        _predictions.Open(Code, 0).Predicting(() => town.PredictLockdownActive(true));
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

        _predictions.Open(Code, 0).Predicting(() => town.PredictLockdownActive(true));
        town.LockdownActive = true;
        _predictions.Refused(Code);

        Assert.True(town.LockdownActive);
    }

    [Fact]
    public void A_guess_nothing_answered_is_taken_back_when_it_runs_out()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.PredictLockdownActive(true));
        _predictions.Expire(NetworkedEventPredictions.Ttl);

        Assert.False(town.LockdownActive);
    }

    /// <summary>One entity's guess is not another's, which is what keying the guess by entity is for.</summary>
    [Fact]
    public void A_guess_about_one_entity_leaves_the_others_alone()
    {
        var guessed = Town();
        var other = Town();

        _predictions.Open(Code, 0).Predicting(() => guessed.PredictLockdownActive(true));

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
            town.PredictLockdownActive(true);
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

        _predictions.Open(Code, 0).Predicting(() => town.PredictLockdownActive(true));

        Assert.True(town.As<Lockdown>().Active);
        Assert.True(town.LockdownActive);
    }

    /// <summary>
    /// A write is a write wherever it is. Guessing is its own call, so nothing has to tell the two apart by
    /// where control came from, and nothing is quietly held back.
    /// </summary>
    [Fact]
    public void A_write_is_a_write_and_reaches_the_value()
    {
        var town = Town();

        town.LockdownActive = true;
        _predictions.Expire(NetworkedEventPredictions.Ttl * 10);

        Assert.True(town.LockdownActive);
    }

    /// <summary>
    /// The server's own value arriving is what answers an accepted guess, long before the deadline would.
    /// Standing one out after the truth has landed is what makes a filtered value show wrong for a while.
    /// </summary>
    [Fact]
    public void The_value_arriving_from_the_server_answers_the_guess()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.PredictLockdownActive(true));
        Assert.True(town.LockdownActive);

        Landed(town);

        Assert.False(town.LockdownActive);
        Assert.Equal(1, _predictions.Outstanding);
    }

    /// <summary>A delta for one entity says nothing about a guess standing on another.</summary>
    [Fact]
    public void A_value_arriving_for_one_entity_leaves_another_entitys_guess_alone()
    {
        var answered = Town();
        var waiting = Town();

        _predictions.Open(Code, 0).Predicting(() =>
        {
            answered.PredictLockdownActive(true);
            waiting.PredictLockdownActive(true);
        });

        Landed(answered);

        Assert.False(answered.LockdownActive);
        Assert.True(waiting.LockdownActive);
    }

    /// <summary>
    /// A delta for a component nothing is guessed in is most of them, and must leave every guess standing.
    /// </summary>
    [Fact]
    public void A_value_arriving_for_another_component_leaves_the_guess_alone()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => town.PredictLockdownActive(true));

        DeltaApplyNotifier.Landed(typeof(HealthComponent), EntityHandle.Of(town).Id);

        Assert.True(town.LockdownActive);
    }

    /// <summary>What the delta path reports once it has written the server's value onto an entity.</summary>
    private static void Landed(Settlement town)
        => DeltaApplyNotifier.Landed(typeof(LockdownComponent), EntityHandle.Of(town).Id);

    /// <summary>
    /// A guess with no raise waiting on an answer would stand for the rest of the session, so it says so
    /// rather than holding a value nothing will take back. The member is named, because the line that made
    /// the guess is not always the line that reads it.
    /// </summary>
    [Fact]
    public void Guessing_with_nothing_waiting_on_an_answer_says_so()
    {
        var town = Town();

        var thrown = Assert.Throws<InvalidOperationException>(() => town.PredictLockdownActive(true));

        Assert.Contains("Lockdown.Active", thrown.Message);
        Assert.False(town.LockdownActive);
    }
}
