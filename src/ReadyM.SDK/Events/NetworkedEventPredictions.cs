using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

/// <summary>
/// The guesses a client is holding while it waits for the server.
/// A guess is taken back by the refusal or by its deadline.
/// </summary>
internal sealed class NetworkedEventPredictions
{
    /// <summary>
    /// How long a prediction stands unanswered, in seconds.
    /// </summary>
    public const double Ttl = 0.5;

    private readonly List<Guess> _outstanding = [];

    /// <summary>How many guesses are still waiting, which a test reads and nothing else does.</summary>
    public int Outstanding => _outstanding.Count;

    /// <summary>A raise that may or may not go on to guess anything.</summary>
    public INetworkedEventRaise Open(RelayMessageCode code, double now) 
        => new Guess(this, code, now + Ttl);

    /// <summary>The server refused an event of this kind, so every guess waiting on one is wrong.</summary>
    public void Refused(RelayMessageCode code) 
        => ClearWhere(guess => guess.Code == code);

    /// <summary>Takes back the guesses nothing answered in time.</summary>
    public void Expire(double now) 
        => ClearWhere(guess => now >= guess.Deadline);

    private void ClearWhere(Func<Guess, bool> stale)
    {
        var taken = _outstanding.Where(stale).ToList();
        _outstanding.RemoveAll(taken.Contains);

        foreach (var guess in taken)
            guess.Undo();
    }

    private sealed class Guess(NetworkedEventPredictions predictions, RelayMessageCode code, double deadline)
        : INetworkedEventRaise
    {
        /// <summary>Puts back the server's values for the guesses this raise made at [Predictable] members.</summary>
        private Action? _drop;

        private Action? _rollback;

        public RelayMessageCode Code { get; } = code;

        public double Deadline { get; } = deadline;

        public INetworkedEventRaise Predicting(Action prediction)
        {
            // Inside here a [Predictable] member's PredictX writes its guess into the component and says how
            // to put the server's value back. A write to anything else is the ordinary write it always is.
            if (NetworkedEventEcsValueGuesses.RunPrediction(prediction) is { } drop)
                _drop += drop;

            if (!predictions._outstanding.Contains(this))
                predictions._outstanding.Add(this);

            return this;
        }

        public INetworkedEventRaise Rollback(Action rollback)
        {
            _rollback = rollback;
            return this;
        }

        public void Undo()
        {
            var drop = _drop;
            var undo = _rollback;
            _drop = null;
            _rollback = null;

            drop?.Invoke();
            undo?.Invoke();
        }
    }
}
