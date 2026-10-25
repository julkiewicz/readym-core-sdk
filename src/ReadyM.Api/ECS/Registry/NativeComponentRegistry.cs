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
internal class NativeComponentRegistry : IdComponentRegistryBase<INativeComponentRegistry, ValueType>, INativeComponentRegistry
{
    public NativeComponentRegistry(IEnumerable<INativeComponentRegistration> registrations)
        : base(registrations)
    {
        // Each type's static Id field gets its id once ids exist (still a stateful static)
        for (var id = 0; id < IdCount; id++)
            GetRegisteredComponent(id).Type!.GetField("Id", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, (byte)id);
    }

    public List<Type> GetComponentTypes()
        => Enumerable.Range(0, IdCount).Select(id => GetRegisteredComponent(id).Type!).ToList();

    public INativeComponentRegistry RegisterComponent<T>(T defaultValue = default) where T : struct
        => RegisterComponentImpl(defaultValue);

    public Type? GetComponentType(int componentId)
        => componentId >= 0 && componentId < IdCount ? GetRegisteredComponent(componentId).Type : null;
}
