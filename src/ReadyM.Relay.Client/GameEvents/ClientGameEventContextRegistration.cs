using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Idents;
using ReadyM.Api.Mapping.Events;
using ReadyM.Api.Multiplayer.ECS.Managers;
using ReadyM.Api.Multiplayer.GameEvents;
using ReadyM.Relay.Client.State;

namespace ReadyM.Relay.Client.GameEvents;

/// <summary>Registers the client's game event contexts: ownership and the master client.</summary>
internal sealed class ClientGameEventContextRegistration(
    ClientState state,
    NetworkedOwnershipManager ownership,
    Store world
) : IGameEventContextRegistration
{
    // NOTE: Ownership compares with the local player entry's id, as ClientOwnershipManager does, not with the relay
    // client's id, which is set earlier during the connection.
    private sealed class LocalPlayerIdProvider(ClientState state) : IPlayerIdProvider
    {
        public PlayerId? PlayerId => state.LocalPlayerId;
    }

    public void Register(GameEventContextRegistry registry)
    {
        registry.Register(new OwnershipContext(ownership, new LocalPlayerIdProvider(state), world));
        registry.Register(new MasterClientContext(state));
    }
}
