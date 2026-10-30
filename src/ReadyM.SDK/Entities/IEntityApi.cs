using Friflo.Engine.ECS;
using ReadyM.Api.Idents;
using ReadyM.Api.Mapping.Data;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Exceptions;
using IComponent = Friflo.Engine.ECS.IComponent;

namespace ReadyM.SDK.Entities;

internal interface IEntityApi
{
    /// <summary>
    /// What this side calls the component type. Asked once per type, see <see cref="ComponentIds{T}"/>.
    /// </summary>
    int ComponentIdOf(Type type);

    /// <summary>Where the component lives, or a default that is not <c>Found</c> if it is absent.</summary>
    /// <exception cref="InvalidEntityException">The entity is gone, or a running query asked for it to be.</exception>
    ComponentRef Locate(RawEntity rawEntity, int componentId);

    /// <summary>
    /// Writes the whole component and moves it in the index. A write through the reference Locate
    /// hands back cannot do that, so an indexed value has to come this way.
    /// </summary>
    void ReplaceIndexed<TComponent, TKey>(RawEntity rawEntity, TComponent component)
        where TComponent : struct, IIndexedComponent<TKey>;

    /// <summary>
    /// Every entity held by the scope that carries the whole component set.
    /// </summary>
    /// <remarks>
    /// Asked of the scope rather than of the world: the entities in one are found through the link
    /// they already carry, so this costs what the scope holds rather than what the world does.
    /// </remarks>
    EntityBuffer CollectInScope(RawEntity scope, ComponentSet components);

    /// <summary>The scope holding this entity, one step up, if it is in one.</summary>
    bool TryGetScope(RawEntity entity, out RawEntity scope);

    /// <summary>The entity whose indexed component holds this value, if one does.</summary>
    bool TryFindByIndex<TComponent, TKey>(TKey key, out RawEntity entity)
        where TComponent : struct, IIndexedComponent<TKey>;

    /// <summary>Whether a write of this kind would be applied to the component.</summary>
    bool Allows(RawEntity rawEntity, Type component, WriteKind kind);

    /// <summary>Whether a mirror of this value would be applied, for a write that carries its own.</summary>
    bool Mirrors<TComponent, TValue>(RawEntity rawEntity, in TComponent component, Field<TComponent, TValue> field)
        where TComponent : struct, IComponent;

    /// <summary>Writes one value, as the game reporting it or as an override of it.</summary>
    /// <returns><c>false</c> when the write does not belong on this side.</returns>
    bool Write<TComponent, TValue>(
        RawEntity rawEntity,
        ref TComponent component,
        Field<TComponent, TValue> field,
        TValue value,
        WriteKind kind)
        where TComponent : struct, IComponent;

    /// <summary>Does this component's data sync direction is from ECS to game?</summary>
    bool ShouldApplyToGame(RawEntity rawEntity, Type component);

    /// <summary>Writes a guess at a predictable value, inside a <c>.Predicting()</c> callback.</summary>
    /// <param name="member">The predictable member, as an exception names it.</param>
    void Predict<TComponent, TValue>(RawEntity rawEntity, Field<TComponent, TValue> field, TValue value, string member)
        where TComponent : struct, INetworkedComponent;

    bool IsAlive(RawEntity rawEntity);

    /// <summary>Whether the entity carries every component of the set. An empty set matches anything.</summary>
    bool HasComponents(RawEntity rawEntity, ComponentSet components);

    /// <param name="owner">
    /// Who this entity belongs to. Null is the default: the server for a server, the local player for a client.
    /// </param>
    /// <exception cref="StructuralChangeInQueryException">A query is running.</exception>
    RawEntity Create(ComponentSet components, RawEntity? scope = null, PlayerId? owner = null);

    /// <summary>
    /// Removes the entity, or, inside a query, records that it is to be removed once the loop ends.
    /// False when it was already gone or already asked for.
    /// </summary>
    bool Delete(RawEntity rawEntity);

    /// <summary>Called by a query as it starts, so deletes inside can be deferred.</summary>
    void EnterQuery();

    /// <summary>Called by a query as it ends, so deletes deferred inside can be applied.</summary>
    void LeaveQuery();
}