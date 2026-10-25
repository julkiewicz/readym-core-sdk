using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ReadyM.Api.Compat;

namespace ReadyM.Api.ECS.Registry;

/// <summary>
/// The components the native side shares. Ids are the base's, in full name order; the native side takes each one's id
/// from its place in <see cref="GetComponentTypes"/>, which is in id order.
/// </summary>
internal class NativeComponentRegistry(IEnumerable<INativeComponentRegistration> registrations)
    : IdComponentRegistryBase<INativeComponentRegistry, ValueType>(registrations), INativeComponentRegistry
{
    // Each type's static Id field gets its id (still a stateful static)
    protected override void OnIdAssigned(byte id, RegisteredComponent component)
        => component.Type!.GetField("Id", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, id);

    public List<Type> GetComponentTypes()
        => RegisteredComponents.Select(component => component.Type!).ToList();

    public INativeComponentRegistry RegisterComponent<T>(T defaultValue = default) where T : struct
        => RegisterComponentImpl(defaultValue);

    public Type? GetComponentType(int componentId)
        => componentId >= 0 && componentId < RegisteredComponents.Count ? RegisteredComponents[componentId].Type : null;
}
