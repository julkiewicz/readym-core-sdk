using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

internal static class NetworkedEventStages
{
    /// <summary>The mods in the order their handlers run, which is name order unless one names another.</summary>
    public static IReadOnlyList<INetworkedEventHandlers<T>> InOrder<T>(IEnumerable<INetworkedEventHandlers<T>> mods)
        where T : struct, INetworkedEvent
    {
        var all = mods.ToList();

        if (all.Count < 2)
            return all;

        var order = NetworkedEventOrdering.Sort([.. all.Select(mod => (mod.Mod, mod.After))]);

        return [.. order.Select(name => all.First(mod => mod.Mod == name))];
    }

    /// <summary>
    /// Runs one stage across every mod, stopping where one cancels and the stage allows it. A stage that can
    /// cancel does not start at all once the event has been cancelled, which is what makes a cancellation skip
    /// every stage after the one it happened in.
    /// </summary>
    public static void Run<T>(IReadOnlyList<INetworkedEventHandlers<T>> mods, Stage stage, ref T networkEvent, EventContext context)
        where T : struct, INetworkedEvent
    {
        var cancellable = CanCancel(stage);

        if (cancellable && context.Cancellation.IsCancelled)
            return;

        context.Cancellation.MarkRunning(stage);

        try
        {
            foreach (var mod in mods)
            {
                mod.Run(stage, ref networkEvent, context);

                if (cancellable && context.Cancellation.IsCancelled)
                    return;
            }
        }
        finally
        {
            context.Cancellation.MarkRunning(null);
        }
    }

    /// <summary>
    /// Whether a cancellation is allowed in this stage.
    /// </summary>
    private static bool CanCancel(Stage stage) => stage switch
    {
        Stage.Validate => true,
        Stage.Filter => true,
        Stage.Apply => false,
        Stage.Observe => false,
        Stage.Rejected => false,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unhandled network event stage."),
    };
}
