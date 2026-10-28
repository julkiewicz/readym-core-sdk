using System.ComponentModel;

namespace ReadyM.SDK.Events;

/// <summary>
/// Where a handler's refusal is recorded, so the dispatcher can skip every stage after it and tell the origin
/// which mod refused and why. Generated code reads it; a mod reaches it through
/// <see cref="EventContext.Cancel{TReason}"/>.
/// </summary>
/// <exclude />
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class EventCancellation
{
    /// <summary>Whether a handler has refused the event.</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>The reason a handler gave, of the type the event declared, or null when it gave none.</summary>
    public object? Reason { get; private set; }

    /// <summary>The manifest id of the mod whose handler refused it.</summary>
    public string StoppedBy { get; private set; } = string.Empty;

    /// <summary>Whose handler is running now, which becomes <see cref="StoppedBy"/> if it refuses.</summary>
    private string _running = string.Empty;

    /// <summary>The stage running now, or null before the first, which is where the policy refuses.</summary>
    private Attributes.Stage? _stage;

    /// <summary>Set by the ladder as each stage starts, so a refusal in one that may not refuse is caught.</summary>
    internal void Running(Attributes.Stage? stage) => _stage = stage;

    /// <exclude />
    /// <summary>
    /// Called by generated dispatch before each handler, so a refusal names the mod without the handler saying
    /// so. A mod reads the answer as <see cref="EventContext.StoppedBy"/>.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public void Entering(string mod) => _running = mod;

    /// <summary>
    /// Refuses the event. The first refusal is the one kept: it is the one the origin is owed, and the handlers
    /// that would have refused after it never run.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The stage running is Apply, Observe or Rejected. By Apply the event has been accepted, and refusing it
    /// then would tell the origin it never happened when it has; by Rejected it was refused already.
    /// </exception>
    public void Cancel(object? reason)
    {
        switch (_stage)
        {
            case Attributes.Stage.Apply:
            case Attributes.Stage.Observe:
                throw new InvalidOperationException(
                    $"'{_running}' refused the event at Stage.{_stage}. Only Stage.Validate and Stage.Filter may "
                    + "refuse: by Apply the event has been accepted, and refusing then would tell the origin it never "
                    + "happened when it has.");

            case Attributes.Stage.Rejected:
                throw new InvalidOperationException(
                    $"'{_running}' refused the event at Stage.Rejected, which runs because it was refused already. "
                    + "Only Stage.Validate and Stage.Filter may refuse.");

            case null:
            case Attributes.Stage.Validate:
            case Attributes.Stage.Filter:
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(_stage), _stage, "Unhandled network event stage.");
        }

        if (IsCancelled)
            return;

        IsCancelled = true;
        Reason = reason;
        StoppedBy = _running;
    }
}
