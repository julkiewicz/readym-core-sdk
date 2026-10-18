using System;
using Friflo.Engine.ECS;

namespace ReadyM.Api.ECS.Registry;

internal interface IAllComponentRegistry : IComponentRegistryBase<IAllComponentRegistry, IComponent>
{
    void RegisterComponent<T>()
        where T : struct, IComponent;
    void RegisterComponent(Type componentType);

    /// <summary>
    /// Registers a component Friflo keeps an index for.
    /// </summary>
    void RegisterIndexedComponent<T, TKey>()
        where T : struct, IIndexedComponent<TKey>
        where TKey : unmanaged;

    /// <summary>Collects a mod-defined component. It has no managed type, so its full type name identifies it.</summary>
    void RegisterModComponent(ModComponentInfo info, string typeFullName);
}
