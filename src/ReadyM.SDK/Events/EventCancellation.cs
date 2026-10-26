namespace ReadyM.SDK.Events;

/// <summary>
/// Where a handler's refusal is recorded, so the dispatcher can skip every stage after it and tell the origin
/// which mod refused and why. Generated code reads it; a mod reaches it through
/// <see cref="EventContext.Cancel{TReason}"/>.
/// </summary>
public sealed class EventCancellation
{
    /// <summary>Whether a handler has refused the event.</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>The reason a handler gave, of the type the event declared, or null when it gave none.</summary>
    public object? Reason { get; private set; }

    /// <summary>The manifest id of the mod whose handler refused it.</summary>
    public string StoppedBy { get; private set; } = string.Empty;

    /// <exclude />
    /// <summary>
    /// Called by generated dispatch before each handler, so a refusal names the mod without the handler saying
    /// so. A mod reads the answer as <see cref="EventContext.StoppedBy"/>.
    /// </summary>
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public void Entering(string mod) => StoppedBy = mod;

    /// <summary>Refuses the event. The first refusal wins, since no later handler runs.</summary>
    public void Cancel(object? reason)
    {
        IsCancelled = true;
        Reason = reason;
    }
}
