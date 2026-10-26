using System.ComponentModel;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// The guesses a client is holding while it waits for the server. A guess is taken back by the refusal or by
/// its deadline, and a deadline is needed because an event the server keeps to itself sends nothing back when
/// it accepts one.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class NetworkEventPredictions
{
    /// <summary>How long a guess stands unanswered, in seconds.</summary>
    public const double Ttl = 0.4;

    private readonly List<Guess> _outstanding = [];

    /// <summary>How many guesses are still waiting, which a test reads and nothing else does.</summary>
    public int Outstanding => _outstanding.Count;

    /// <summary>A raise that may or may not go on to guess anything.</summary>
    public INetworkEventRaise Open(RelayMessageCode code, double now) => new Guess(this, code, now + Ttl);

    /// <summary>The server refused an event of this kind, so every guess waiting on one is wrong.</summary>
    public void Refused(RelayMessageCode code) => Clear(guess => guess.Code == code);

    /// <summary>Takes back the guesses nothing answered in time.</summary>
    public void Expire(double now) => Clear(guess => now >= guess.Deadline);

    private void Clear(Func<Guess, bool> stale)
    {
        // Taken out first: an undo may raise again, and a list being walked must not grow underneath it
        var taken = _outstanding.Where(stale).ToList();
        _outstanding.RemoveAll(guess => taken.Contains(guess));

        foreach (var guess in taken)
            guess.Undo();
    }

    private sealed class Guess(NetworkEventPredictions predictions, RelayMessageCode code, double deadline)
        : INetworkEventRaise
    {
        /// <summary>What drops the guessed values a [Predictable] member's setter held for this raise.</summary>
        private Action? _drop;

        private Action? _undo;

        public RelayMessageCode Code { get; } = code;

        public double Deadline { get; } = deadline;

        public INetworkEventRaise Predicting(Action guess)
        {
            // Inside here a [Predictable] member's setter holds the value beside the entity rather than
            // writing it, and says how to drop it again. A guess at anything else writes as it always does.
            if (NetworkEventGuesses.Take(guess) is { } drop)
                _drop += drop;

            if (!predictions._outstanding.Contains(this))
                predictions._outstanding.Add(this);

            return this;
        }

        public INetworkEventRaise Rollback(Action undo)
        {
            _undo = undo;

            return this;
        }

        public void Undo()
        {
            var drop = _drop;
            var undo = _undo;
            _drop = null;
            _undo = null;

            drop?.Invoke();
            undo?.Invoke();
        }
    }
}
