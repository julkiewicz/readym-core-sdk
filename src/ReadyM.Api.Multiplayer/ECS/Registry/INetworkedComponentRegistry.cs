using System;
using LiteNetLib;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.Interop;

namespace ReadyM.Api.Multiplayer.ECS.Registry;

internal interface INetworkedComponentRegistry : IComponentRegistryBase<INetworkedComponentRegistry, INetworkedComponent>
{
    INetworkedComponentRegistry RegisterComponent(Type componentTyp, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered);
    INetworkedComponentRegistry RegisterComponent<T>(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        where T : struct, INetworkedComponent;
    INetworkedComponentRegistry RegisterModComponent(ModNetworkedComponentInfo info, string typeFullName);

    /// <summary>What a mod handed over for its networked component, registered under its full type name.</summary>
    ModNetworkedComponentInfo GetModComponentInfo(string typeFullName);

    NetworkedComponentId GetNetworkedComponentId(Type type);
    NetworkedComponentId GetNetworkedComponentId<T>();
    NetworkedComponentId GetNetworkedComponentId(string typeFullName);
    Type GetComponentType(NetworkedComponentId componentId);
    DeliveryMethod GetNetworkedComponentDeliveryMethod<T>();
    DeliveryMethod GetNetworkedComponentDeliveryMethod(string typeFullName);
}
