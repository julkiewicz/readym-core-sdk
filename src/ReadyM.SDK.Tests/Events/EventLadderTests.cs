using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Events;
using Xunit;

namespace ReadyM.SDK.Tests.Events;

/// <summary>
/// The state generated dispatch carries through a network event's stages: who raised it, whether this machine
/// did, and where a refusal is recorded so every later stage is skipped and the origin learns who refused.
/// </summary>
public class EventLadderTests
{
    private static readonly PlayerId Raiser = new(7);

    // -- what a refusal records -----------------------------------------------------------------------

    [Fact]
    public void A_fresh_cancellation_has_refused_nothing()
    {
        var cancellation = new EventCancellation();

        Assert.False(cancellation.IsCancelled);
        Assert.Null(cancellation.Reason);
        Assert.Equal(string.Empty, cancellation.StoppedBy);
    }

    [Fact]
    public void Cancelling_with_a_reason_keeps_it()
    {
        var cancellation = new EventCancellation();

        cancellation.Cancel(Refusal.Untargetable);

        Assert.True(cancellation.IsCancelled);
        Assert.Equal(Refusal.Untargetable, cancellation.Reason);
    }

    /// <summary>An event whose declaration named no reject type is still refusable, just without a reason.</summary>
    [Fact]
    public void Cancelling_without_a_reason_still_refuses()
    {
        var cancellation = new EventCancellation();

        cancellation.Cancel(null);

        Assert.True(cancellation.IsCancelled);
        Assert.Null(cancellation.Reason);
    }

    /// <summary>
    /// No handler runs after a refusal, so a second one cannot happen through the ladder. If one ever does, the
    /// first reason is the one the origin is owed.
    /// </summary>
    [Fact]
    public void The_first_refusal_is_the_one_that_is_kept()
    {
        var cancellation = new EventCancellation();

        cancellation.Cancel(Refusal.Untargetable);
        cancellation.Cancel(Refusal.OutOfRange);

        Assert.Equal(Refusal.Untargetable, cancellation.Reason);
    }

    /// <summary>A refusal names its author without the handler having to say so.</summary>
    [Fact]
    public void The_mod_whose_handler_was_running_is_the_one_blamed()
    {
        var cancellation = new EventCancellation();

        cancellation.Entering("Other.Rules");
        cancellation.Entering("Other.Censor");
        cancellation.Cancel(Refusal.Untargetable);

        Assert.Equal("Other.Censor", cancellation.StoppedBy);
    }

    // -- what a handler is told -----------------------------------------------------------------------

    [Fact]
    public void The_context_carries_who_raised_it_and_whether_this_machine_did()
    {
        var here = new EventContext(Raiser, true, new EventCancellation());
        var elsewhere = new EventContext(Raiser, false, new EventCancellation());

        Assert.Equal(Raiser, here.Origin);
        Assert.True(here.RaisedHere);
        Assert.False(elsewhere.RaisedHere);
    }

    [Fact]
    public void Cancelling_through_the_context_refuses_the_event()
    {
        var cancellation = new EventCancellation();
        var ctx = new EventContext(Raiser, false, cancellation);

        ctx.Cancel(Refusal.OutOfRange);

        Assert.True(cancellation.IsCancelled);
        Assert.Equal(Refusal.OutOfRange, cancellation.Reason);
    }

    [Fact]
    public void A_rejected_handler_reads_who_refused_through_the_context()
    {
        var cancellation = new EventCancellation();
        var ctx = new EventContext(Raiser, false, cancellation);

        cancellation.Entering("Other.Censor");
        ctx.Cancel(Refusal.Untargetable);

        Assert.Equal("Other.Censor", ctx.StoppedBy);
    }

    // -- the audience the server picks ------------------------------------------------------------------

    [Fact]
    public void A_server_audience_names_nobody_else()
        => Assert.Equal(Scope.Server, EventAudience.Server.Kind);

    [Fact]
    public void A_global_audience_names_every_player()
        => Assert.Equal(Scope.Global, EventAudience.Global.Kind);

    [Fact]
    public void A_single_audience_carries_the_one_player()
    {
        var audience = EventAudience.Single(Raiser);

        Assert.Equal(Scope.Single, audience.Kind);
        Assert.Equal(Raiser, audience.Player);
    }

    [Fact]
    public void An_area_audience_carries_the_area()
    {
        var area = new AreaId(3);
        var audience = EventAudience.InArea(area);

        Assert.Equal(Scope.Area, audience.Kind);
        Assert.Equal(area, audience.Area);
    }

    [Fact]
    public void A_cell_audience_carries_the_cell()
    {
        var cell = new FullCellId(new AreaId(3), new CellId("cell-1"));
        var audience = EventAudience.InCell(cell);

        Assert.Equal(Scope.Cell, audience.Kind);
        Assert.Equal(cell, audience.Cell);
    }

    private enum Refusal
    {
        Untargetable,
        OutOfRange,
    }
}
