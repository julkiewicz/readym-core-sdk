using System.ComponentModel;
using Friflo.Engine.ECS;

namespace ReadyM.SDK.Archetypes;

/// <summary>Finds an entity by key in the index of the component a shape names through <see cref="IIndexed{TKey}"/>.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IIndexFinder<TKey>
{
    bool TryFind<TComponent>(TKey key, out RawEntity entity)
        where TComponent : struct, IIndexedComponent<TKey>;
}
