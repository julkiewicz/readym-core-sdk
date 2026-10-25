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
    private readonly Dictionary<string, Type> _types = new();

    public NativeComponentRegistry(IEnumerable<INativeComponentRegistration> registrations)
        : base(registrations)
    {
        // Each type's static Id field gets its id once ids exist (still a stateful static)
        foreach (var entry in _types)
            entry.Value.GetField("Id", BindingFlags.Static | BindingFlags.Public)?.SetValue(null, GetId(entry.Key));
    }

    public List<Type> GetComponentTypes()
        => Enumerable.Range(0, IdCount).Select(id => _types[GetTypeFullName(id)]).ToList();

    public INativeComponentRegistry RegisterComponent<T>(T defaultValue = default) where T : struct
    {
        _types.Add(typeof(T).FullName!, typeof(T));
        return base.RegisterComponentImpl(defaultValue);
    }

    public Type? GetComponentType(int componentId)
        => componentId >= 0 && componentId < IdCount ? _types[GetTypeFullName(componentId)] : null;
}
