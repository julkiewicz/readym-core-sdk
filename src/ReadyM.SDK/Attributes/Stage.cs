namespace ReadyM.SDK.Attributes;

/// <summary>
/// When a networked event handler runs. Stages run in this order and stopping one skips every later stage;
/// Only one of <see cref="Observe"/> and <see cref="Rejected"/> runs, based on whether the event was cancelled or not.
/// </summary>
public enum Stage
{
    /// <summary>Runs before any other stage. May cancel the event, but cannot rewrite its fields.</summary>
    Validate,

    /// <summary>Runs after <see cref="Validate"/>. May rewrite event members, may cancel the event.</summary>
    Filter,

    /// <summary>Runs if the event was not cancelled. Write main event handling logic here. Multiple handlers are allowed, but no guarantee of order is provided.</summary>
    Apply,

    /// <summary>If the event was not cancelled, runs after <see cref="Apply"/>.</summary>
    Observe,

    /// <summary>Runs if the event was cancelled.</summary>
    Rejected,
}
