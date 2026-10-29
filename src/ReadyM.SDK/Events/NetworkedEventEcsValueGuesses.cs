using System.ComponentModel;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// Whether a component field guess is being taken right now, which is what tells a generated setter to hold the value beside
/// the entity instead of writing it into the replicated component.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class NetworkedEventEcsValueGuesses
{
    [ThreadStatic]
    private static List<Action>? _drops;

    /// <summary>Whether this thread is inside a component field guess.</summary>
    public static bool InsidePrediction => _drops is not null;

    /// <summary>Generated code says here how to take back what it has just put somewhere.</summary>
    public static void PushRollback(Action rollback) => _drops?.Add(rollback);

    /// <summary>
    /// Runs the guess and hands back a handler to drop guessed values (or null when none).
    /// </summary>
    internal static Action? RunPrediction(Action guess)
    {
        var outerDrops = _drops;
        var drops = new List<Action>();
        _drops = drops;

        try
        {
            guess();
        }
        finally
        {
            _drops = outerDrops;
        }

        if (drops.Count == 0)
            return null;

        return () => drops.ForEach(drop => drop());
    }
}