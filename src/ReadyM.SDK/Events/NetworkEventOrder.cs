using System.ComponentModel;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// The order mods' handlers for one event run in. A mod names what it must follow; everything else keeps its
/// load order, so a set of mods that say nothing about each other behaves the same every run.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkEventOrder
{
    /// <exception cref="InvalidOperationException">The mods name a cycle, which has no order to run in.</exception>
    public static List<string> Sort(IReadOnlyList<(string Mod, IReadOnlyList<string> After)> mods)
    {
        var after = mods.ToDictionary(m => m.Mod, m => m.After);
        var order = new List<string>(mods.Count);
        var done = new HashSet<string>();
        var open = new HashSet<string>();

        foreach (var (mod, _) in mods)
            Visit(mod, after, order, done, open);

        return order;
    }

    private static void Visit(
        string mod,
        Dictionary<string, IReadOnlyList<string>> after,
        List<string> order,
        HashSet<string> done,
        HashSet<string> open)
    {
        if (done.Contains(mod))
            return;

        if (!open.Add(mod))
            throw new InvalidOperationException(
                $"Mods {string.Join(", ", open.OrderBy(m => m, StringComparer.Ordinal))} name each other in "
                + "After, so there is no order their handlers can run in. Break the cycle in one of them.");

        // A mod that is not loaded is nothing to run behind, which is what an optional dependency looks like
        foreach (var earlier in after[mod].Where(after.ContainsKey))
            Visit(earlier, after, order, done, open);

        open.Remove(mod);
        done.Add(mod);
        order.Add(mod);
    }
}
