using System.ComponentModel;

namespace ReadyM.SDK.Archetypes;

/// <summary>
/// Represents a set of component types that make up an archetype or mixin.
/// A general class like this is required since the client uses ComponentTypes and the server uses numeric IDs.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ComponentSet
{
    public static readonly ComponentSet Empty = new([]);

    private ComponentSet(Type[] types, bool split = true, bool replicated = false)
    {
        Types = types;
        Replicated = replicated;

        if (!split || types.Length <= 1)
        {
            Head = this;
            return;
        }

        Head = new ComponentSet([types[0]], split: false);
        Rest = new ComponentSet(types.Skip(1).ToArray(), split: false);
    }

    internal bool Replicated { get; }

    internal Type[] Types { get; }

    /// The component that is most likely to be unique in an archetype, to make lookup faster.
    internal ComponentSet Head { get; }

    /// <summary>Everything after it, so confirming a match does not ask about the head twice.</summary>
    internal ComponentSet? Rest { get; }

    /// <summary>How many components the set holds, which is how many chunk slots it occupies.</summary>
    public int Count => Types.Length;

    public static ComponentSet Combine(params ComponentSet[] sets)
    {
        var types = Distinct(sets);

        return types.Length == 0 ? Empty : new ComponentSet(types);
    }

    public static ComponentSet Archetype(bool replicated, params ComponentSet[] sets)
        => new(Distinct(sets), replicated: replicated);

    private static Type[] Distinct(ComponentSet[] sets)
    {
        var types = new List<Type>();

        foreach (var set in sets)
        foreach (var type in set.Types)
            if (!types.Contains(type))
                types.Add(type);

        return [.. types];
    }

    public override string ToString() => string.Join(", ", Types.Select(type => type.Name));

    #region Static helpers

    public static ComponentSet Of<T1>()
        where T1 : struct
        => Cache<T1>.Set;

    public static ComponentSet Of<T1, T2>()
        where T1 : struct where T2 : struct
        => Cache<T1, T2>.Set;

    public static ComponentSet Of<T1, T2, T3>()
        where T1 : struct where T2 : struct where T3 : struct
        => Cache<T1, T2, T3>.Set;

    public static ComponentSet Of<T1, T2, T3, T4>()
        where T1 : struct where T2 : struct where T3 : struct where T4 : struct
        => Cache<T1, T2, T3, T4>.Set;

    public static ComponentSet Of<T1, T2, T3, T4, T5>()
        where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct
        => Cache<T1, T2, T3, T4, T5>.Set;

    public static ComponentSet Of<T1, T2, T3, T4, T5, T6>()
        where T1 : struct where T2 : struct where T3 : struct where T4 : struct where T5 : struct where T6 : struct
        => Cache<T1, T2, T3, T4, T5, T6>.Set;

    private static class Cache<T1>
    {
        internal static readonly ComponentSet Set = new([typeof(T1)]);
    }

    private static class Cache<T1, T2>
    {
        internal static readonly ComponentSet Set = new([typeof(T1), typeof(T2)]);
    }

    private static class Cache<T1, T2, T3>
    {
        internal static readonly ComponentSet Set = new([typeof(T1), typeof(T2), typeof(T3)]);
    }

    private static class Cache<T1, T2, T3, T4>
    {
        internal static readonly ComponentSet Set = new([typeof(T1), typeof(T2), typeof(T3), typeof(T4)]);
    }

    private static class Cache<T1, T2, T3, T4, T5>
    {
        internal static readonly ComponentSet Set = new([typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5)]);
    }

    private static class Cache<T1, T2, T3, T4, T5, T6>
    {
        internal static readonly ComponentSet Set = new([typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5), typeof(T6)]);
    }

    #endregion
}