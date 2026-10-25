using System.ComponentModel;
using LiteNetLib;
using ReadyM.Api.DI;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Archetypes;

/// <summary>
/// Registers one replicated component on the networked twin, with how its changes travel. Public and stateless:
/// generated code in any mod assembly calls it, and the registration interfaces are internal to the SDK.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ReplicationRegistrationHelper
{
    public static void Register<T>(IDependencyContainer services, Delivery delivery)
        where T : struct, INetworkedComponent
        => services.RegisterSingleton<INetworkedComponentRegistration>(new ReplicationRegistration<T>(ToDeliveryMethod(delivery)));

    // Reliable means ordered as well, the same reading the relay gives it.
    private static DeliveryMethod ToDeliveryMethod(Delivery delivery) => delivery switch
    {
        Delivery.Unreliable => DeliveryMethod.Unreliable,
        _ => DeliveryMethod.ReliableOrdered
    };

    private sealed class ReplicationRegistration<T>(DeliveryMethod deliveryMethod) : INetworkedComponentRegistration
        where T : struct, INetworkedComponent
    {
        // Explicit, because the registry type is internal to the multiplayer API
        void IComponentRegistrationBase<INetworkedComponentRegistry, INetworkedComponent>.Register(INetworkedComponentRegistry registry)
            => registry.RegisterComponent<T>(deliveryMethod);
    }
}
