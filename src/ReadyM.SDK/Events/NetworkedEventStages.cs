using System;
using System.Collections.Generic;
using System.Linq;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <summary>
/// The ladder both sides' coordinators run: every mod's handlers for one stage, then the next stage. Shared so
/// that the two cannot disagree about the order mods run in, which would leave a client applying an event the
/// server had already filtered differently.
/// </summary>
internal static class NetworkedEventStages
{
    /// <summary>The mods in the order their handlers run, which is name order unless one names another.</summary>
    public static IReadOnlyList<INetworkedEventHandlers<T>> InOrder<T>(IEnumerable<INetworkedEventHandlers<T>> mods)
        where T : struct, INetworkedEvent
    {
        var all = mods.ToList();

        if (all.Count < 2)
            return all;

        var order = NetworkedEventOrder.Sort([.. all.Select(mod => (mod.Mod, mod.After))]);

        return [.. order.Select(name => all.First(mod => mod.Mod == name))];
    }

    /// <summary>
    /// Runs one stage across every mod, stopping where one refuses and the stage allows it. A stage that can
    /// be stopped does not start at all once the event has been refused, which is what makes a refusal skip
    /// every stage after the one it happened in.
    /// </summary>
    public static void Run<T>(
        IReadOnlyList<INetworkedEventHandlers<T>> mods, Stage stage, ref T networkEvent, EventContext context)
        where T : struct, INetworkedEvent
    {
        var stoppable = Stops(stage);

        if (stoppable && context.Cancellation.IsCancelled)
            return;

        context.Cancellation.Running(stage);

        try
        {
            foreach (var mod in mods)
            {
                mod.Run(stage, ref networkEvent, context);

                if (stoppable && context.Cancellation.IsCancelled)
                    return;
            }
        }
        finally
        {
            context.Cancellation.Running(null);
        }
    }

    /// <summary>
    /// Whether a refusal in this stage stops the event. The two that may are the two that run before anything
    /// has happened; refusing after the effect has landed would leave the sides holding different worlds.
    /// </summary>
    private static bool Stops(Stage stage) => stage switch
    {
        Stage.Validate => true,
        Stage.Filter => true,
        Stage.Apply => false,
        Stage.Observe => false,
        Stage.Rejected => false,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unhandled network event stage."),
    };
}
