using System;
using Friflo.Engine.ECS;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.ECS.Managers;
using ReadyM.Api.State;

namespace ReadyM.Relay.Client.State;

internal class ClientNetworkedEntityState(
    ClientState state,
    INetworkedEntityManager netEntity) : IClientEntityManager
{
    public Entity CreateEntity(
        ArchetypeId archetypeId,
        Entity? scopeEntity,
        Action<EntityBuilder>? setComponents = null,
        PlayerId? ownerOverride = null)
    {
        EnsureConnected();
        var (entity, _) = netEntity.CreateNetworkedEntity(archetypeId, scopeEntity, setComponents, ownerOverride);
        return entity;
    }

    public Entity CreateGlobalEntity(
        ArchetypeId archetypeId,
        Action<EntityBuilder>? setComponents = null,
        PlayerId? ownerOverride = null)
        => CreateEntity(archetypeId, null, setComponents, ownerOverride);

    public Entity CreateAreaEntity(
        ArchetypeId archetypeId,
        Action<EntityBuilder>? setComponents = null,
        PlayerId? ownerOverride = null)
    {
        EnsureConnected();
        if (!state.CurrentAreaEntity.HasValue)
            throw new InvalidOperationException("Attempted to create a networked entity in area but no area is set.");

        var scopeEntity = state.CurrentAreaEntity.Value;
        var (entity, _) = netEntity.CreateNetworkedEntity(archetypeId, scopeEntity, setComponents, ownerOverride);
        return entity;
    }

    public Entity CreateCellEntity(
        CellId cellId,
        ArchetypeId archetypeId,
        Action<EntityBuilder>? setComponents = null,
        PlayerId? ownerOverride = null)
    {
        EnsureConnected();
        var cellEntry = state.GetActiveCellEntry(cellId);
        if (!cellEntry.HasValue)
            throw new InvalidOperationException($"Attempted to create a networked entity in cell {cellId} but that cell is not active.");

        var scopeEntity = cellEntry.Value.CellEntity;
        var (entity, _) = netEntity.CreateNetworkedEntity(archetypeId, scopeEntity, setComponents, ownerOverride);
        return entity;
    }

    public Entity CreatePlayerEntity(ArchetypeId archetypeId)
    {
        EnsureConnected();
        if (state.LocalPlayerEntity == null)
            throw new InvalidOperationException("Attempted to create a networked entity for player but no player entity is set.");

        var scopeEntity = state.LocalPlayerEntity.Value;
        var (entity, _) = netEntity.CreateNetworkedEntity(archetypeId, scopeEntity);
        return entity;
    }

    // ClientState counts as connected once the global snapshot is in; an entity created before then would come back in it.
    private void EnsureConnected()
    {
        if (!state.IsConnected)
            throw new InvalidOperationException("Attempted to create a networked entity before being connected.");
    }
}
