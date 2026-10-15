using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using ReadyM.Api.Saves;

namespace ReadyM.SDK.Archetypes;

/// Every component a persisted shape had generated.
[EditorBrowsable(EditorBrowsableState.Never)]
public static class PersistenceRegistry
{
    private static readonly ConcurrentDictionary<Type, PersistedComponent> Components = new();

    /// Called by generated code for a persisted shape.
    public static void Register<TComponent>()
        where TComponent : struct, ISavableComponent
        => Components[typeof(TComponent)] = new PersistedComponent<TComponent>();

    public static IReadOnlyList<PersistedComponent> Snapshot()
        => [.. Components.Values];

    public static bool TryGet(Type component, [NotNullWhen(true)] out PersistedComponent? persisted)
        => Components.TryGetValue(component, out persisted);

    /// Only for tests, which need each case to start from nothing.
    internal static void Clear() => Components.Clear();
}

/// A persisted component, handed back to an acceptor with its type as a generic argument.
public abstract class PersistedComponent
{
    public abstract Type Component { get; }

    public abstract void Accept(IPersistedComponentCallback callback);
}

public interface IPersistedComponentCallback
{
    void Accept<T>() where T : struct, ISavableComponent;
}

internal sealed class PersistedComponent<T> : PersistedComponent
    where T : struct, ISavableComponent
{
    public override Type Component => typeof(T);

    public override void Accept(IPersistedComponentCallback callback) => callback.Accept<T>();
}
