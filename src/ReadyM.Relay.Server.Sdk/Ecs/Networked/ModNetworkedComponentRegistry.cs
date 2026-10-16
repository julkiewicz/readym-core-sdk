using LiteNetLib;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Api.Multiplayer.Interop;

namespace ReadyM.Relay.Server.Sdk.Ecs.Networked;

/// <summary>
/// The networked components the loaded mods declare, with how each one travels. The mod-side counterpart of the
/// relay's <see cref="NetworkedComponentRegistry"/>, built the same way: from every
/// <see cref="INetworkedComponentRegistration"/> in the container, and sealed once constructed.
/// <para>
/// Holds types and delivery only. Wire ids are the relay's to give out, once every mod has declared, so asking
/// for one here is a mistake rather than a question.
/// </para>
/// </summary>
internal sealed class ModNetworkedComponentRegistry(IEnumerable<INetworkedComponentRegistration> registrations)
    : ComponentRegistryBase<INetworkedComponentRegistry, INetworkedComponent>(registrations), INetworkedComponentRegistry
{
    private readonly Dictionary<string, DeliveryMethod> _deliveryMethods = new(StringComparer.Ordinal);

    public INetworkedComponentRegistry RegisterComponent(Type componentType, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        if (!typeof(INetworkedComponent).IsAssignableFrom(componentType))
            throw new ArgumentException($"Type {componentType.FullName} does not implement INetworkedComponent", nameof(componentType));

        Record(componentType, deliveryMethod);
        return RegisterComponentImpl(componentType);
    }

    public INetworkedComponentRegistry RegisterComponent<T>(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        where T : struct, INetworkedComponent
    {
        Record(typeof(T), deliveryMethod);
        return RegisterComponentImpl(default(T));
    }

    private void Record(Type componentType, DeliveryMethod deliveryMethod)
    {
        NetworkedComponentRegistry.RequireDeltaSafe(deliveryMethod, componentType.FullName!);
        NetworkedComponentRegistry.RequireInternedSafe(componentType, deliveryMethod);

        // Added after the checks above, so a component they refuse never looks registered.
        _deliveryMethods.Add(componentType.FullName!, deliveryMethod);
    }

    public DeliveryMethod GetNetworkedComponentDeliveryMethod<T>()
        => _deliveryMethods[typeof(T).FullName!];

    public DeliveryMethod GetNetworkedComponentDeliveryMethod(string typeFullName)
        => _deliveryMethods[typeFullName];

    public INetworkedComponentRegistry RegisterModComponent(ModNetworkedComponentInfo info, string typeFullName)
        => throw Unreachable(nameof(RegisterModComponent));

    public ModNetworkedComponentInfo GetModComponentInfo(string typeFullName)
        => throw Unreachable(nameof(GetModComponentInfo));

    public NetworkedComponentId GetNetworkedComponentId(Type type)
        => throw Unreachable(nameof(GetNetworkedComponentId));

    public NetworkedComponentId GetNetworkedComponentId<T>()
        => throw Unreachable(nameof(GetNetworkedComponentId));

    public NetworkedComponentId GetNetworkedComponentId(string typeFullName)
        => throw Unreachable(nameof(GetNetworkedComponentId));

    public Type GetComponentType(NetworkedComponentId componentId)
        => throw Unreachable(nameof(GetComponentType));

    private static NotSupportedException Unreachable(string member)
        => new($"{member} is not supported on the mod side: every component here is the mod's own, and wire ids are "
            + "given out by the relay once every mod has declared.");
}
