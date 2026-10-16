using ReadyM.Api.Multiplayer.ECS.Registry;

namespace ReadyM.Relay.Server.Sdk.Ecs.Networked;

/// <summary>
/// Contributes the callbacks the relay replicates mod components through. The mod-side counterpart of the relay's
/// <c>INetworkedComponentJobRegistration</c>.
/// </summary>
internal interface IModNetworkedComponentPointerRegistration
{
    void Register(INetworkedComponentRegistry registry, ModNetworkedComponentPointerRegistry pointerRegistry);
}
