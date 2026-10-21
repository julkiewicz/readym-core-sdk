using LiteNetLib;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Api.Multiplayer.Interop;

namespace ReadyM.Relay.Server.Sdk.Ecs.Components;

/// <summary>
/// The networked twin: the list of every networked component the mods declared, with each one's delivery method.
/// A list and nothing else; it hands out no ids, those stay the AOT side's.
/// </summary>
internal sealed class ModNetworkedComponentRegistry(IEnumerable<INetworkedComponentRegistration> registrations)
    : ComponentRegistryBase<INetworkedComponentRegistry, INetworkedComponent>(registrations), INetworkedComponentRegistry
{
    private readonly Dictionary<string, DeliveryMethod> _deliveryMethods = new();

    public INetworkedComponentRegistry RegisterComponent<T>(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        where T : struct, INetworkedComponent
    {
        NetworkedComponentRegistry.RequireDeltaSafe(deliveryMethod, typeof(T).FullName!);
        NetworkedComponentRegistry.RequireInternedSafe(typeof(T), deliveryMethod);
        _deliveryMethods.Add(typeof(T).FullName!, deliveryMethod);
        return RegisterComponentImpl(default(T));
    }

    public INetworkedComponentRegistry RegisterComponent(Type componentType, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        if (!typeof(INetworkedComponent).IsAssignableFrom(componentType))
            throw new ArgumentException($"Type {componentType.FullName} does not implement INetworkedComponent", nameof(componentType));

        NetworkedComponentRegistry.RequireDeltaSafe(deliveryMethod, componentType.FullName!);
        NetworkedComponentRegistry.RequireInternedSafe(componentType, deliveryMethod);
        _deliveryMethods.Add(componentType.FullName!, deliveryMethod);
        return RegisterComponentImpl(componentType);
    }

    public DeliveryMethod GetNetworkedComponentDeliveryMethod<T>()
        => GetNetworkedComponentDeliveryMethod(typeof(T).FullName!);

    public DeliveryMethod GetNetworkedComponentDeliveryMethod(string typeFullName)
    {
        ThrowIfNotSealed(typeFullName);
        return _deliveryMethods[typeFullName];
    }

    public INetworkedComponentRegistry RegisterModComponent(ModNetworkedComponentInfo info, string typeFullName, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        => throw OnlyOnTheAotSide(typeFullName);

    public ModNetworkedComponentInfo GetModComponentInfo(string typeFullName)
        => throw OnlyOnTheAotSide(typeFullName);

    // FixMe(2026-10-18-twins-m4): no id reaches this side yet, so these throw. They answer from what Bind step 3 hands
    // over once a reader on this side needs them.
    public NetworkedComponentId GetNetworkedComponentId(Type type)
        => throw NoIdsYet(type.FullName!);

    public NetworkedComponentId GetNetworkedComponentId<T>()
        => throw NoIdsYet(typeof(T).FullName!);

    public NetworkedComponentId GetNetworkedComponentId(string typeFullName)
        => throw NoIdsYet(typeFullName);

    public Type GetComponentType(NetworkedComponentId componentId)
        => throw NoIdsYet(componentId.ToString());

    private static NotSupportedException OnlyOnTheAotSide(string typeFullName)
        => new($"Mod components with ready-made pointers only exist on the AOT side; {typeFullName} is registered by its type here.");

    private static NotSupportedException NoIdsYet(string what)
        => new($"Networked component ids are handed out on the AOT side and none reach the managed mod side yet ({what}).");
}
