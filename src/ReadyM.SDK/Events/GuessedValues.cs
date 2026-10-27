using System.ComponentModel;
using ReadyM.Api.Multiplayer.ECS.Jobs;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// Where every predictable member's guesses hear that the server's own value has arrived. One subscription to
/// the delta path for the process, rather than one per member, so the path costs the same whatever a mod
/// declares.
/// </summary>
/// <remarks>
/// Game thread only. The list is built as the accessor classes are first touched, which is before any entity
/// exists to guess about, so nothing walks it while it grows.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GuessedValues
{
    private static readonly List<(Type Component, Action<int> Answered)> Held = [];

    private static bool _watching;

    /// <summary>Called once per predictable member, as its accessor class is first touched.</summary>
    internal static void Holds(Type component, Action<int> answered)
    {
        Held.Add((component, answered));

        if (_watching)
            return;

        _watching = true;
        DeltaApplyNotifier.Watch(Landed);
    }

    private static void Landed(Type component, int entity)
    {
        // Walked rather than looked up: a process holds a handful of predictable members, and a dictionary of
        // lists would cost an allocation per member to save a comparison per delta.
        foreach (var (held, answered) in Held)
            if (held == component)
                answered(entity);
    }
}
