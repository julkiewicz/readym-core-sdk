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
public sealed class NetworkedEventPredictions
{
    /// <summary>
    /// How long a guess stands unanswered, in seconds. A fallback, not the way a guess normally ends: the
    /// refusal takes back a refused one and the server's own value takes back an accepted one, both of which
    /// arrive over a reliable channel well inside this. Reaching it means the player's connection is in
    /// trouble, and holding a guess any longer than that would not help them.
    /// </summary>
    public const double Ttl = 0.5;

    private readonly List<Guess> _outstanding = [];

    /// <summary>How many guesses are still waiting, which a test reads and nothing else does.</summary>
    public int Outstanding => _outstanding.Count;

    /// <summary>A raise that may or may not go on to guess anything.</summary>
    public INetworkedEventRaise Open(RelayMessageCode code, double now) => new Guess(this, code, now + Ttl);

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

    private sealed class Guess(NetworkedEventPredictions predictions, RelayMessageCode code, double deadline)
        : INetworkedEventRaise
    {
        /// <summary>What puts back the server's values for the guesses this raise made at [Predictable] members.</summary>
        private Action? _drop;

        private Action? _undo;

        public RelayMessageCode Code { get; } = code;

        public double Deadline { get; } = deadline;

        public INetworkedEventRaise Predicting(Action guess)
        {
            // Inside here a [Predictable] member's PredictX writes its guess into the component and says how
            // to put the server's value back. A write to anything else is the ordinary write it always is.
            if (NetworkedEventGuesses.Take(guess) is { } drop)
                _drop += drop;

            if (!predictions._outstanding.Contains(this))
                predictions._outstanding.Add(this);

            return this;
        }

        public INetworkedEventRaise Rollback(Action undo)
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
