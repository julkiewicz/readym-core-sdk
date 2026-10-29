using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Events;
using ReadyM.SDK.Tests.Client;
using ReadyM.SDK.Tests.Client.Fixtures;

namespace ReadyM.SDK.Tests.Events;

/// <summary>
/// What a [Predictable] member's generated accessors stand on: the guess written into the component, the
/// server's value kept aside, and the server's value put back when the guess is taken back.
/// </summary>
/// <remarks>
/// Driven on a number rather than a flag, so which server value came back is visible. A registry is made here
/// by hand over a field the fixture does not mark, which is all the generated code does over one it does.
/// </remarks>
public class GuessedValueTests : ClientSdkTest
{
    private const RelayMessageCode Code = RelayMessageCode.MaxServerRpcEvent;

    private readonly NetworkedEventPredictions _predictions = new();

    /// <summary>
    /// Every guess taken back before the world goes. The registries are static, so a guess left standing
    /// would still be listening to the delta path in the next test.
    /// </summary>
    protected override void TearDown() => _predictions.Expire(double.MaxValue);

    private readonly GuessedValueRegistry<LockdownComponent, int> _hour = new("Curfew.Hour", LockdownComponent.Fields.Hour);

    private Settlement Town(int hour = 5)
    {
        var town = Entities.Create<Settlement>();

        town.LockdownHour = hour;
        EntityHandle.Of(town).GetComponent<LockdownComponent>().ClearDirty();

        return town;
    }

    private void Guess(Settlement town, int hour) => _hour.Guess(EntityHandle.Of(town), hour);

    [Fact]
    public void A_guess_reads_back_as_the_value()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => Guess(town, 6));

        Assert.Equal(6, town.LockdownHour);
    }

    /// <summary>A guess made twice in one raise stands as the last one, and the first one's start is kept.</summary>
    [Fact]
    public void Guessing_twice_in_one_raise_puts_back_the_value_from_before_the_first()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() =>
        {
            Guess(town, 6);
            Guess(town, 7);
        });

        Assert.Equal(7, town.LockdownHour);

        _predictions.Refused(Code);

        Assert.Equal(5, town.LockdownHour);
    }

    /// <summary>
    /// A raise guessing on top of a standing guess joins it rather than restarting its clock. The stack goes
    /// when the raise that started it runs out, so it stands at most one window however many raises add to it.
    /// </summary>
    [Fact]
    public void The_first_raise_running_out_takes_back_the_whole_stack()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => Guess(town, 6));
        _predictions.Open(Code, 0.3).Predicting(() => Guess(town, 7));

        // Only the first one's deadline has passed
        _predictions.Expire(NetworkedEventPredictions.Ttl);

        Assert.Equal(5, town.LockdownHour);
    }

    /// <summary>
    /// The server's value is the one from before any guess stood. The second raise found the first one's
    /// guess in the component, and keeping that would put a guess back as if the server had said it.
    /// </summary>
    [Fact]
    public void A_guess_on_top_of_a_guess_puts_back_the_servers_value_not_the_first_guess()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => Guess(town, 6));
        _predictions.Open(Code, 0.3).Predicting(() => Guess(town, 7));

        _predictions.Expire(0.3 + NetworkedEventPredictions.Ttl);

        Assert.Equal(5, town.LockdownHour);
    }

    /// <summary>
    /// Damage over time with nothing answering it, drowning say: a raise every tick, each guessing a step on
    /// from the last. Were each raise to restart the clock the stack would never end and the guess would run
    /// all the way down. It goes back to the server's value once a window has passed, and the next raise
    /// starts a stack of its own from there.
    /// </summary>
    [Fact]
    public void A_steady_stream_of_raises_is_taken_back_once_every_window()
    {
        var town = Town(hour: 20);

        for (var tick = 0; tick < 5; tick++)
            _predictions.Open(Code, tick * 0.1).Predicting(() => Guess(town, town.LockdownHour - 1));

        Assert.Equal(15, town.LockdownHour);

        _predictions.Expire(NetworkedEventPredictions.Ttl);

        Assert.Equal(20, town.LockdownHour);

        _predictions.Open(Code, 0.5).Predicting(() => Guess(town, town.LockdownHour - 1));
        _predictions.Expire(0.9);

        Assert.Equal(19, town.LockdownHour);

        _predictions.Expire(0.5 + NetworkedEventPredictions.Ttl);

        Assert.Equal(20, town.LockdownHour);
    }

    /// <summary>Each member has its own registry, so a guess at one leaves the other as the server left it.</summary>
    [Fact]
    public void A_guess_at_one_member_says_nothing_about_another()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => Guess(town, 6));

        Assert.False(town.LockdownActive);

        _predictions.Refused(Code);

        Assert.False(town.LockdownActive);
        Assert.Equal(5, town.LockdownHour);
    }

    /// <summary>
    /// Nothing is waiting on an answer out here, so the guess would stand for the rest of the session. It
    /// says so rather than holding a value nothing will ever take back, and leaves the value alone.
    /// </summary>
    [Fact]
    public void Guessing_with_no_raise_waiting_on_an_answer_says_so()
    {
        var town = Town();

        var thrown = Assert.Throws<InvalidOperationException>(() => Guess(town, 6));

        Assert.Contains("Curfew.Hour", thrown.Message);
        Assert.Contains("Predicting", thrown.Message);
        Assert.Equal(5, town.LockdownHour);
    }

    /// <summary>
    /// The scope is the callback and nothing after it, so a guess a step outside it is as unanswered as one
    /// made anywhere else.
    /// </summary>
    [Fact]
    public void Guessing_after_the_callback_has_run_is_outside_it()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => Guess(town, 6));

        Assert.Throws<InvalidOperationException>(() => Guess(town, 7));
    }

    /// <summary>
    /// A guessed value needs no rollback of its own, but a raise that guesses one may still roll an action
    /// back, and both have to happen.
    /// </summary>
    [Fact]
    public void A_raise_that_guesses_a_value_and_an_action_takes_both_back()
    {
        var town = Town();
        var ran = new List<string>();

        _predictions.Open(Code, 0)
            .Predicting(() => Guess(town, 6))
            .Rollback(() => ran.Add("undo"));

        _predictions.Refused(Code);

        Assert.Equal(["undo"], ran);
        Assert.Equal(5, town.LockdownHour);
    }

    /// <summary>
    /// The entity going away while its guess stands is ordinary, say a creature dying to the hit being
    /// guessed at. There is nothing to put back, and the deadline coming round must not fall over it.
    /// </summary>
    [Fact]
    public void A_guess_on_an_entity_that_has_gone_is_dropped_quietly()
    {
        var town = Town();

        _predictions.Open(Code, 0).Predicting(() => Guess(town, 6));
        Entities.Delete(town);

        _predictions.Expire(NetworkedEventPredictions.Ttl);

        Assert.Equal(0, _predictions.Outstanding);
    }
}
