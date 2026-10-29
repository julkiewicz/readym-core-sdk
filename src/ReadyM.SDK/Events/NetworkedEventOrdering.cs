namespace ReadyM.SDK.Events;

/// <summary>
/// The order mods' handlers for one event run in. A mod names what it must follow; everything else runs in the
/// order of the mods' own names.
/// </summary>
internal static class NetworkedEventOrdering
{
    /// <exception cref="InvalidOperationException">The mods name a cycle, which has no order to run in.</exception>
    public static List<string> Sort(IReadOnlyList<(string Mod, IReadOnlyList<string> After)> mods)
    {
        var after = mods.ToDictionary(m => m.Mod, m => m.After);
        var order = new List<string>(mods.Count);
        var done = new HashSet<string>();
        var open = new HashSet<string>();

        foreach (var mod in after.Keys.OrderBy(name => name, StringComparer.Ordinal))
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
