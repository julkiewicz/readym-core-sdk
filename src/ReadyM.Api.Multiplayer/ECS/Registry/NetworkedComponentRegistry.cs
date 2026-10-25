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
internal sealed class NetworkedComponentRegistry
    : IdComponentRegistryBase<INetworkedComponentRegistry, INetworkedComponent, NetworkedComponentContext>, INetworkedComponentRegistry
{
    public NetworkedComponentRegistry(IEnumerable<INetworkedComponentRegistration> registrations, ILogger logger)
        : base(registrations)
    {
        for (var id = 0; id < IdCount; id++)
        {
            var registered = GetRegisteredComponent(id);
            logger.LogInformation("Registered networked component {Id}: {ComponentFullName} delivery {DeliveryMethod}",
                new NetworkedComponentId((byte)id), registered.TypeFullName, registered.Context.DeliveryMethod);
        }
    }

    public INetworkedComponentRegistry RegisterComponent(Type componentType, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
    {
        RequireDeltaSafe(deliveryMethod, componentType.FullName!);
        RequireInternedSafe(componentType, deliveryMethod);

        return RegisterComponentImpl(componentType, new NetworkedComponentContext(deliveryMethod, default));
    }

    public INetworkedComponentRegistry RegisterComponent<T>(DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        where T : struct, INetworkedComponent
    {
        RequireDeltaSafe(deliveryMethod, typeof(T).FullName!);
        RequireInternedSafe(typeof(T), deliveryMethod);

        return RegisterComponentImpl(new NetworkedComponentContext(deliveryMethod, default), default(T));
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

        // No networked acceptor reads the storage struct; the networked pointers are read through GetModComponentInfo
        return RegisterModComponentImpl(new NetworkedComponentContext(deliveryMethod, info), default, typeFullName);
    }

    public ModNetworkedComponentInfo GetModComponentInfo(string typeFullName)
        => GetContext(typeFullName).ModComponentInfo;

    public NetworkedComponentId GetNetworkedComponentId(Type type)
        => new(GetId(type.FullName!));

    public NetworkedComponentId GetNetworkedComponentId<T>()
        => new(GetId(typeof(T).FullName!));

    public NetworkedComponentId GetNetworkedComponentId(string typeFullName)
        => new(GetId(typeFullName));

    public Type GetComponentType(NetworkedComponentId componentId)
        => GetRegisteredComponent(componentId.RawValue).Type!;

    public DeliveryMethod GetNetworkedComponentDeliveryMethod<T>()
        => GetNetworkedComponentDeliveryMethod(typeof(T).FullName!);

    public DeliveryMethod GetNetworkedComponentDeliveryMethod(string typeFullName)
        => GetContext(typeFullName).DeliveryMethod;

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