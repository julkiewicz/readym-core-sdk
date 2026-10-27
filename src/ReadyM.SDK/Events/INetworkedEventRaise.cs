namespace ReadyM.SDK.Events;

/// <summary>
/// A raised event, so a client can act on it before the server has answered. The guess is taken back by the
/// refusal or by its own deadline, whichever comes first.
/// </summary>
public interface INetworkedEventRaise
{
    /// <summary>
    /// Runs <paramref name="guess"/> now, as though the event had been accepted. A guessed VALUE needs nothing
    /// else: the read falls through to the replicated one when the guess clears. A guessed ACTION needs
    /// <see cref="Rollback"/>, because only the caller knows how to undo it.
    /// </summary>
    INetworkedEventRaise Predicting(Action guess);

    /// <summary>Undoes the guess, on the refusal or the deadline. Runs once.</summary>
    INetworkedEventRaise Rollback(Action undo);
}
