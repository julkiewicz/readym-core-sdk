using System.Collections.Generic;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Events;
using ReadyM.SDK.Tests.Client;
using Xunit;

namespace ReadyM.SDK.Tests.Events;

/// <summary>
/// What a [Predictable] member's generated accessor stands on: a value held beside the entity while a guess is
/// outstanding, read in front of the replicated one, and dropped when the guess is taken back.
/// </summary>
/// <remarks>
/// The stand-in for the component here is a dictionary the test writes itself, which is what lets the
/// fall-through be asserted: the point of dropping rather than restoring is that the server's value as it
/// stands at that moment is what the read falls through to.
/// </remarks>
public class GuessedValueTests : ClientSdkTest
{
    private const RelayMessageCode Code = RelayMessageCode.MaxServerRpcEvent;

    private readonly NetworkEventPredictions _predictions = new();

    private readonly GuessedValueRegistry<bool> _frozen = new();

    private readonly Dictionary<EntityHandle, bool> _replicated = new();

    private EntityHandle Entity() => EntityHandle.Of(SpawnMonster());

    /// <summary>What a generated getter does: the guess if there is one, otherwise the replicated value.</summary>
    private bool Read(EntityHandle handle)
        => _frozen.TryGet(handle, out var guessed) ? guessed : _replicated.GetValueOrDefault(handle);

    /// <summary>What a generated setter does: hold it as a guess, or write it through.</summary>
    private void Write(EntityHandle handle, bool value)
    {
        if (_frozen.Guessing(handle, value))
            return;

        _replicated[handle] = value;
    }

    [Fact]
    public void A_write_outside_a_guess_goes_through_to_the_value_itself()
    {
        var entity = Entity();

        Write(entity, true);

        Assert.True(_replicated[entity]);
        Assert.True(Read(entity));
    }

    [Fact]
    public void A_write_inside_a_guess_is_read_back_without_touching_the_value_itself()
    {
        var entity = Entity();

        _predictions.Open(Code, 0).Predicting(() => Write(entity, true));

        Assert.True(Read(entity));
        Assert.False(_replicated.ContainsKey(entity));
    }

    [Fact]
    public void A_refusal_drops_the_guess_and_the_read_falls_through()
    {
        var entity = Entity();

        _predictions.Open(Code, 0).Predicting(() => Write(entity, true));
        _predictions.Refused(Code);

        Assert.False(Read(entity));
    }

    [Fact]
    public void A_guess_nothing_answered_is_dropped_when_it_runs_out()
    {
        var entity = Entity();

        _predictions.Open(Code, 0).Predicting(() => Write(entity, true));
        _predictions.Expire(NetworkEventPredictions.Ttl);

        Assert.False(Read(entity));
    }

    /// <summary>
    /// The reason clearing drops the guess rather than putting a value back. The server accepting the event is
    /// an ordinary replicated change that lands while the guess still stands, and the read has to end up on
    /// it, not on what was true when the guess was made.
    /// </summary>
    [Fact]
    public void An_authoritative_change_that_landed_meanwhile_is_not_clobbered()
    {
        var entity = Entity();

        _predictions.Open(Code, 0).Predicting(() => Write(entity, true));
        _replicated[entity] = true;
        _predictions.Expire(NetworkEventPredictions.Ttl);

        Assert.True(Read(entity));
    }

    [Fact]
    public void A_guess_about_one_entity_says_nothing_about_another()
    {
        var guessed = Entity();
        var other = Entity();

        _predictions.Open(Code, 0).Predicting(() => Write(guessed, true));

        Assert.True(Read(guessed));
        Assert.False(Read(other));
    }

    /// <summary>
    /// Each member has its own registry, so one guess cannot be read as another's. Two members of one shape
    /// sharing a store keyed by entity would hand a read of the second one the first one's value.
    /// </summary>
    [Fact]
    public void A_guess_at_one_member_says_nothing_about_another()
    {
        var entity = Entity();
        var other = new GuessedValueRegistry<bool>();

        _predictions.Open(Code, 0).Predicting(() => Write(entity, true));

        Assert.False(other.TryGet(entity, out _));
    }

    /// <summary>A guess made twice in one raise stands as the last one, and is still dropped once.</summary>
    [Fact]
    public void Guessing_twice_leaves_the_last_guess_and_drops_it_once()
    {
        var entity = Entity();

        _predictions.Open(Code, 0).Predicting(() =>
        {
            Write(entity, true);
            Write(entity, false);
        });

        Assert.False(Read(entity));

        _replicated[entity] = true;
        _predictions.Refused(Code);

        Assert.True(Read(entity));
    }

    /// <summary>
    /// The scope is the callback and nothing after it, or a later write meaning something else entirely would
    /// be held back and silently never reach the entity.
    /// </summary>
    [Fact]
    public void A_write_after_the_guess_has_run_is_not_part_of_it()
    {
        var entity = Entity();

        _predictions.Open(Code, 0).Predicting(() => Write(entity, true));
        Write(entity, true);

        Assert.True(_replicated[entity]);
    }

    /// <summary>
    /// A guessed value needs no rollback of its own, but a raise that guesses one may still roll an action
    /// back, and both have to happen.
    /// </summary>
    [Fact]
    public void A_raise_that_guesses_a_value_and_an_action_takes_both_back()
    {
        var entity = Entity();
        var ran = new List<string>();

        _predictions.Open(Code, 0)
            .Predicting(() => Write(entity, true))
            .Rollback(() => ran.Add("undo"));

        _predictions.Refused(Code);

        Assert.Equal(["undo"], ran);
        Assert.False(Read(entity));
    }
}
