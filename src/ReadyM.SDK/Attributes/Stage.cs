namespace ReadyM.SDK.Attributes;

/// <summary>
/// When a handler runs. Stages run in this order and stopping one skips every later stage;
/// <see cref="Observe"/> and <see cref="Rejected"/> are the two endings and exactly one of them runs.
/// </summary>
public enum Stage
{
    /// <summary>Is this allowed. May stop the event, may not change it.</summary>
    Validate,

    /// <summary>Change it. May rewrite members, may stop the event.</summary>
    Filter,

    /// <summary>The effect. The event's owner writes this one.</summary>
    Apply,

    /// <summary>It happened. Sees the final values, cannot stop it.</summary>
    Observe,

    /// <summary>It did not happen. Sees the reason, typed as the event's declared reject type.</summary>
    Rejected,
}
