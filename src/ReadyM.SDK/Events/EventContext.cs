using System.ComponentModel;
using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <summary>What a network event handler is told about the event besides its members.</summary>
public readonly struct EventContext
{
    /// <summary>Built by generated code, once per event it dispatches.</summary>
    /// <exclude />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EventContext(PlayerId origin, EventCancellation cancellation)
    {
        Origin = origin;
        Cancellation = cancellation;
    }

    /// <summary>Who raised it: a player, or <see cref="PlayerId.Server"/>.</summary>
    public PlayerId Origin { get; }

    /// <summary>Where a refusal is recorded.</summary>
    /// <exclude />
    [EditorBrowsable(EditorBrowsableState.Never)]
    public EventCancellation Cancellation { get; }

    /// <summary>On <c>Stage.Rejected</c>, the manifest id of the mod that refused it.</summary>
    public string StoppedBy => Cancellation.StoppedBy;

    /// <summary>
    /// Refuses the event, with a reason of the type its declaration named. Every later stage is skipped and the
    /// reason reaches the origin.
    /// </summary>
    public void Cancel<TReason>(TReason reason) => Cancellation.Cancel(reason);

    /// <summary>Refuses the event without a reason. Every later stage is skipped.</summary>
    public void Cancel() => Cancellation.Cancel(null);
}
