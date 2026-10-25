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
    : ComponentRegistryBase<INetworkedComponentRegistry, INetworkedComponent, DeliveryMethod>(registrations), INetworkedComponentRegistry
{
    public INetworkedComponentRegistry RegisterComponent<T>(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        where T : struct, INetworkedComponent
        => RegisterComponentImpl(deliveryMethod, default(T));

    public INetworkedComponentRegistry RegisterComponent(Type componentType, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        => RegisterComponentImpl(componentType, deliveryMethod);

    /// <summary>Both overloads land here: refuses a delivery method T's deltas cannot survive, then records it.</summary>
    protected override INetworkedComponentRegistry RegisterComponentImpl<T>(DeliveryMethod deliveryMethod, T defaultValue = default)
    {
        NetworkedComponentRegistry.RequireDeltaSafe(deliveryMethod, typeof(T).FullName!);
        NetworkedComponentRegistry.RequireInternedSafe(typeof(T), deliveryMethod);

        return base.RegisterComponentImpl(deliveryMethod, defaultValue);
    }

    public DeliveryMethod GetNetworkedComponentDeliveryMethod<T>()
        => GetNetworkedComponentDeliveryMethod(typeof(T).FullName!);

    public DeliveryMethod GetNetworkedComponentDeliveryMethod(string typeFullName)
        => GetContext(typeFullName);

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
