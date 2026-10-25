using Friflo.Engine.ECS;
using ReadyM.SDK.Archetypes;

namespace ReadyM.SDK.Entities;

/// <summary>Finds by key through the entity api, for the component an indexed shape names.</summary>
internal readonly struct EntityApiIndexFinder<TKey>(IEntityApi api) : IIndexFinder<TKey>
{
    public bool TryFind<TComponent>(TKey key, out RawEntity entity)
        where TComponent : struct, IIndexedComponent<TKey>
        => api.TryFindByIndex<TComponent, TKey>(key, out entity);
}
