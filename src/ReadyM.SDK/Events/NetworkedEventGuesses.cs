using System.ComponentModel;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// Whether a guess is being made right now, which is what tells a generated setter to hold the value beside
/// the entity instead of writing it into the replicated component.
/// </summary>
/// <remarks>
/// Thread-static because a guess is one call on one thread: the scope lasts exactly as long as the callback
/// handed to <see cref="INetworkEventRaise.Predicting"/>, and anything that callback starts elsewhere is not
/// part of the guess.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkEventGuesses
{
    [ThreadStatic]
    private static List<Action>? _taking;

    /// <summary>Whether this thread is inside a guess.</summary>
    public static bool Taking => _taking is not null;

    /// <summary>Generated code says here how to take back what it has just put somewhere.</summary>
    public static void Took(Action undo) => _taking?.Add(undo);

    /// <summary>
    /// Runs the guess, and hands back what takes everything it wrote back out, or null where it wrote nothing
    /// a generated setter knew about and so has only whatever the caller rolls back itself.
    /// </summary>
    internal static Action? Take(Action guess)
    {
        var outer = _taking;
        var taken = new List<Action>();
        _taking = taken;

        try
        {
            guess();
        }
        finally
        {
            _taking = outer;
        }

        if (taken.Count == 0)
            return null;

        return () =>
        {
            foreach (var undo in taken)
                undo();
        };
    }
}
