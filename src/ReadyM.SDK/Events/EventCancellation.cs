using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <summary>Records the first cancellation of an event: which mod cancelled it, and why.</summary>
internal sealed class EventCancellation
{
    private string _currentMod = string.Empty;

    private Stage? _currentStage;

    public bool IsCancelled { get; private set; }

    /// <summary>The reason the cancelling handler gave, of the type the event declared, or null.</summary>
    public object? Reason { get; private set; }

    /// <summary>The unique ID of the mod that cancelled the event.</summary>
    public string CancelledBy { get; private set; } = string.Empty;

    /// <summary>An event that is already cancelled: by the policy, or by the server before it reached a client.</summary>
    public static EventCancellation Cancelled(string cancelledBy, object? reason)
        => new() { IsCancelled = true, CancelledBy = cancelledBy, Reason = reason };

    public void SetCurrentStage(Stage? stage) => _currentStage = stage;

    public void SetCurrentMod(string mod) => _currentMod = mod;

    /// <summary>Cancels the event, unless it is cancelled already, in which case the first reason stands.</summary>
    /// <exception cref="InvalidOperationException">The current stage is Apply, Observe or Rejected.</exception>
    public void Cancel(object? reason)
    {
        switch (_currentStage)
        {
            case Stage.Apply:
            case Stage.Observe:
            case Stage.Rejected:
                throw new InvalidOperationException(
                    $"'{_currentMod}' cancelled the event at Stage.{_currentStage}. Only Stage.Validate and "
                    + "Stage.Filter may cancel an event.");

            case null:
            case Stage.Validate:
            case Stage.Filter:
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(_currentStage), _currentStage, "Unhandled networked event stage.");
        }

        if (IsCancelled)
            return;

        IsCancelled = true;
        Reason = reason;
        CancelledBy = _currentMod;
    }
}
