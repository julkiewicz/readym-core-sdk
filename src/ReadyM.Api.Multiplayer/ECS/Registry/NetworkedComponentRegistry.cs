using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using LiteNetLib;
using Microsoft.Extensions.Logging;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.Interning;
using ReadyM.Api.Multiplayer.Interop;

namespace ReadyM.Api.Multiplayer.ECS.Registry;

/// <summary>
/// What is networked about each component. Ids are the base's, in full name order, so the client and the server get the
/// same ids from the same set of networked components whatever order their registrations ran in.
/// </summary>
internal sealed class NetworkedComponentRegistry : IdComponentRegistryBase<INetworkedComponentRegistry, INetworkedComponent>, INetworkedComponentRegistry
{
    private readonly Dictionary<string, DeliveryMethod> _deliveryMethods = new();
    private readonly Dictionary<string, Type> _types = new();   // typed components only
    private readonly Dictionary<string, ModNetworkedComponentInfo> _modComponentInfos = new();

    public NetworkedComponentRegistry(IEnumerable<INetworkedComponentRegistration> registrations, ILogger logger)
        : base(registrations)
    {
        for (var id = 0; id < IdCount; id++)
        {
            var typeFullName = GetTypeFullName(id);
            logger.LogInformation("Registered networked component {Id}: {ComponentFullName} delivery {DeliveryMethod}",
                new NetworkedComponentId((byte)id), typeFullName, _deliveryMethods[typeFullName]);
        }
    }

    public INetworkedComponentRegistry RegisterComponent(Type componentType, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        if (!typeof(INetworkedComponent).IsAssignableFrom(componentType))
            throw new ArgumentException($"Type {componentType.FullName} does not implement INetworkedComponent", nameof(componentType));

        RequireDeltaSafe(deliveryMethod, componentType.FullName!);
        RequireInternedSafe(componentType, deliveryMethod);

        _deliveryMethods.Add(componentType.FullName!, deliveryMethod);
        _types.Add(componentType.FullName!, componentType);
        return base.RegisterComponentImpl(componentType);
    }

    public INetworkedComponentRegistry RegisterComponent<T>(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        where T : struct, INetworkedComponent
    {
        RequireDeltaSafe(deliveryMethod, typeof(T).FullName!);
        RequireInternedSafe(typeof(T), deliveryMethod);

        _deliveryMethods.Add(typeof(T).FullName!, deliveryMethod);
        _types.Add(typeof(T).FullName!, typeof(T));
        return base.RegisterComponentImpl(default(T));
    }

    /// <summary>
    /// A component defined by a mod. It has no managed type here, so its full type name is its key, and it
    /// reaches acceptors through <c>AcceptModComponent</c>.
    /// </summary>
    public INetworkedComponentRegistry RegisterModComponent(
        ModNetworkedComponentInfo info,
        string typeFullName,
        DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        RequireDeltaSafe(deliveryMethod, typeFullName);

        _deliveryMethods.Add(typeFullName, deliveryMethod);
        _modComponentInfos.Add(typeFullName, info);

        // No networked acceptor reads the storage struct; the networked pointers are read through GetModComponentInfo
        return RegisterModComponentImpl(default, typeFullName);
    }

    public ModNetworkedComponentInfo GetModComponentInfo(string typeFullName)
    {
        ThrowIfNotSealed(typeFullName);
        return _modComponentInfos[typeFullName];
    }

    public NetworkedComponentId GetNetworkedComponentId(Type type)
        => new(GetId(type.FullName!));

    public NetworkedComponentId GetNetworkedComponentId<T>()
        => new(GetId(typeof(T).FullName!));

    public NetworkedComponentId GetNetworkedComponentId(string typeFullName)
        => new(GetId(typeFullName));

    public Type GetComponentType(NetworkedComponentId componentId)
        => _types[GetTypeFullName(componentId.RawValue)];

    public DeliveryMethod GetNetworkedComponentDeliveryMethod<T>()
        => GetNetworkedComponentDeliveryMethod(typeof(T).FullName!);

    public DeliveryMethod GetNetworkedComponentDeliveryMethod(string typeFullName)
    {
        ThrowIfNotSealed(typeFullName);
        return _deliveryMethods[typeFullName];
    }

    // A delta carries only the entities and fields that changed since the previous one, all of it on one channel,
    // and nothing sends it again. Sequenced drops every message but the newest and unordered can apply an older
    // value over a newer one, either of which leaves the state wrong until that field changes again
    internal static void RequireDeltaSafe(DeliveryMethod deliveryMethod, string component)
    {
        if (deliveryMethod is DeliveryMethod.Unreliable or DeliveryMethod.ReliableOrdered)
            return;

        throw new ArgumentException(
            $"{component} is registered with {deliveryMethod}. A networked component takes Unreliable, for a value that is " +
            "sent again whenever it changes, or ReliableOrdered for anything else.",
            nameof(deliveryMethod));
    }

    // An id is only readable after the reliable-ordered message carrying its string, and an unreliable delta can
    // overtake that message
    internal static void RequireInternedSafe(Type componentType, DeliveryMethod deliveryMethod)
    {
        if (deliveryMethod == DeliveryMethod.ReliableOrdered || !typeof(IInternedComponent).IsAssignableFrom(componentType))
            return;

        throw new ArgumentException(
            $"{componentType.FullName} is registered with {deliveryMethod}, but has a field marked [Interned], which " +
            "needs ReliableOrdered.",
            nameof(deliveryMethod));
    }
}