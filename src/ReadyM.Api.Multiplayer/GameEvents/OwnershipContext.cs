using Friflo.Engine.ECS;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Multiplayer.ECS.Managers;

namespace ReadyM.Api.Multiplayer.GameEvents;

/// <summary>
/// The game event context for ownership: whether this machine owns an entity. Read by the policies of events marked
/// <c>[OwnershipBased]</c> and <c>[RunOnMasterClientOnly]</c>.
/// </summary>
public readonly struct OwnershipContext
{
    private readonly NetworkedOwnershipManager _ownership;
    private readonly IPlayerIdProvider _self;
    private readonly Store _world;

    internal OwnershipContext(NetworkedOwnershipManager ownership, IPlayerIdProvider self, Store world)
    {
        _ownership = ownership;
        _self = self;
        _world = world;
    }

    public bool OwnsEntity(Entity entity)
        => _ownership.TryGetOwner(entity, out var ownerId) && ownerId == _self.PlayerId;

    public bool OwnsEntity(RawEntity entity)
    {
        var resolved = _world.GetEntityByRawEntity(entity);
        return !resolved.IsNull && OwnsEntity(resolved);
    }
}
