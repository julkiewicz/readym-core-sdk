using LiteNetLib;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Server;

/// <summary>
/// Tells the mod-side networked registry which components the loaded shapes replicate, and how each travels.
/// The server counterpart of the client's registration of the same name.
/// </summary>
internal sealed class ReplicatedShapeRegistration : INetworkedComponentRegistration
{
    public void Register(INetworkedComponentRegistry registry)
    {
        foreach (var replicated in ReplicationRegistry.InWireOrder())
            registry.RegisterComponent(replicated.Component, Wire(replicated.Delivery));
    }

    // Reliable means ordered as well, the same reading the relay gives it.
    private static DeliveryMethod Wire(Delivery delivery) => delivery switch
    {
        Delivery.Unreliable => DeliveryMethod.Unreliable,
        _ => DeliveryMethod.ReliableOrdered
    };
}
