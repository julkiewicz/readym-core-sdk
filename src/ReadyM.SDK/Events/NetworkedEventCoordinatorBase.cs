using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <summary>Runs every mod's handlers for one event, one stage at a time.</summary>
internal abstract class NetworkedEventCoordinatorBase<T>(IEnumerable<INetworkedEventHandlers<T>> handlers)
    where T : struct, INetworkedEvent
{
    private readonly IReadOnlyList<INetworkedEventHandlers<T>> _handlers = SortByAfter([.. handlers]);

    /// <summary>
    /// Runs one stage across every mod. Validate and Filter stop at the first cancellation, and do not start at
    /// all on an event that is already cancelled.
    /// </summary>
    protected void RunStage(Stage stage, ref T networkEvent, EventContext context)
    {
        var cancellable = CanCancel(stage);

        if (cancellable && context.IsCancelled)
            return;

        context.Cancellation.SetCurrentStage(stage);

        try
        {
            foreach (var mod in _handlers)
            {
                context.Cancellation.SetCurrentMod(mod.Mod);
                mod.Run(stage, ref networkEvent, context);

                if (cancellable && context.IsCancelled)
                    return;
            }
        }
        finally
        {
            context.Cancellation.SetCurrentStage(null);
        }
    }

    private static bool CanCancel(Stage stage) => stage switch
    {
        Stage.Validate => true,
        Stage.Filter => true,
        Stage.Apply => false,
        Stage.Observe => false,
        Stage.Rejected => false,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unhandled networked event stage."),
    };

    /// <summary>
    /// Orders the mods by name, then moves each one behind the mods it names in <c>After</c>. The order must be
    /// the same on the server and every client, which the order the mods loaded in is not.
    /// </summary>
    /// <exception cref="InvalidOperationException">The mods name each other in a cycle.</exception>
    internal static List<INetworkedEventHandlers<T>> SortByAfter(List<INetworkedEventHandlers<T>> mods)
    {
        var byName = mods.ToDictionary(mod => mod.Mod);
        var sorted = new List<INetworkedEventHandlers<T>>(mods.Count);
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var name in byName.Keys.OrderBy(name => name, StringComparer.Ordinal))
            Visit(name);

        return sorted;

        void Visit(string name)
        {
            if (visited.Contains(name))
                return;

            if (!visiting.Add(name))
                throw new InvalidOperationException(
                    $"Mods {string.Join(", ", visiting.OrderBy(mod => mod, StringComparer.Ordinal))} name each other "
                    + "in After, so their handlers have no order to run in.");

            // A mod that is not loaded has nothing to run behind
            foreach (var earlier in byName[name].After.Where(byName.ContainsKey))
                Visit(earlier);

            visiting.Remove(name);
            visited.Add(name);
            sorted.Add(byName[name]);
        }
    }
}
