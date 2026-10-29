namespace ReadyM.SDK.Events;

/// <summary>
/// Allows providing prediction and rollback for a raised event.
/// </summary>
public interface INetworkedEventRaise
{
    /// <summary>
    /// Runs <paramref name="prediction"/> now, as though the event had been accepted.
    /// Any changes to archetype values will be rolled back automatically if the event is refused or times out.
    /// Any other action needs <see cref="Rollback"/>, because only the caller knows how to undo it.
    /// </summary>
    INetworkedEventRaise Predicting(Action prediction);

    /// <summary>Undoes the prediction, on the refusal or the deadline. Runs once.</summary>
    INetworkedEventRaise Rollback(Action rollback);
}
