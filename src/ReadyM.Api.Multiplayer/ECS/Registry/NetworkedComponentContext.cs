using LiteNetLib;
using ReadyM.Api.Multiplayer.Interop;

namespace ReadyM.Api.Multiplayer.ECS.Registry;

/// <summary>What a networked component is registered with: how its changes travel, and a mod one's networked pointers.</summary>
internal readonly record struct NetworkedComponentContext(DeliveryMethod DeliveryMethod, ModNetworkedComponentInfo ModComponentInfo);
