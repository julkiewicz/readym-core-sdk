using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

/// <summary>
/// The raises this client predicted and is waiting on the server for. A prediction is rolled back when the server
/// refuses the event, or when it times out.
/// </summary>
internal sealed class PredictedRaiseRegistry(GuessedValueRegistry guessedValues)
{
    /// <summary>How long a prediction stands unanswered, in seconds.</summary>
    public const double Timeout = 0.5;

    private readonly GuessedValueRegistry _guessedValues = guessedValues;

    private readonly List<PredictedRaise> _pending = [];

    /// <summary>How many predictions are waiting on the server.</summary>
    public int PendingCount => _pending.Count;

    /// <summary>Starts a raise, which may or may not go on to predict anything.</summary>
    public INetworkedEventRaise StartRaise(RelayMessageCode code, double now) => new PredictedRaise(this, code, now + Timeout);

    /// <summary>The server refused an event with this code, so every prediction waiting on one is wrong.</summary>
    public void RollBackRefused(RelayMessageCode code) => RollBackWhere(raise => raise.Code == code);

    public void RollBackExpired(double now) => RollBackWhere(raise => now >= raise.Deadline);

    private void RollBackWhere(Func<PredictedRaise, bool> matches)
    {
        var matched = _pending.Where(matches).ToList();
        _pending.RemoveAll(matched.Contains);

        foreach (var raise in matched)
            raise.RollBack();
    }

    private sealed class PredictedRaise(PredictedRaiseRegistry registry, RelayMessageCode code, double deadline)
        : INetworkedEventRaise
    {
        private Action? _restoreGuessedValues;

        private Action? _customRollback;

        public RelayMessageCode Code { get; } = code;

        public double Deadline { get; } = deadline;

        public INetworkedEventRaise Predicting(Action prediction)
        {
            if (registry._guessedValues.RunPrediction(prediction) is { } restore)
                _restoreGuessedValues += restore;

            if (!registry._pending.Contains(this))
                registry._pending.Add(this);

            return this;
        }

        public INetworkedEventRaise Rollback(Action rollback)
        {
            _customRollback = rollback;
            return this;
        }

        public void RollBack()
        {
            var restore = _restoreGuessedValues;
            var custom = _customRollback;
            _restoreGuessedValues = null;
            _customRollback = null;

            restore?.Invoke();
            custom?.Invoke();
        }
    }
}
