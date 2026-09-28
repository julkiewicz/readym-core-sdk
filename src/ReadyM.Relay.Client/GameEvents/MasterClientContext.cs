using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Relay.Client.State;

namespace ReadyM.Relay.Client.GameEvents;

/// <summary>
/// The game event context for the master client: whether this client is the master of its current area. Read by
/// the policies of events marked <c>[MasterClientManaged]</c> and <c>[RunOnMasterClientOnly]</c>.
/// </summary>
public readonly struct MasterClientContext
{
    private readonly ClientState _state;

    internal MasterClientContext(ClientState state)
    {
        _state = state;
    }

    public bool IsMasterClient
    {
        get
        {
            var area = _state.CurrentAreaEntity;
            return area is { } entity
                   && _state.LocalPlayerId is { } local
                   && entity.GetComponent<AreaScopeComponent>().MasterClient == local;
        }
    }
}
