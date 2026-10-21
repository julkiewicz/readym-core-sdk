using Friflo.Engine.ECS;
using ReadyM.Api.ECS.Registry;

namespace ReadyM.Relay.Server.Sdk.Ecs.Components;

/// <summary>
/// The "all" twin: the list of every component the mods declared, on <see cref="ComponentRegistryBase{TRegistry,TComponent}"/>
/// like the AOT side's AllComponentRegistry. A list and nothing else; its consumers do the work.
/// </summary>
internal sealed class ModAllComponentRegistry(IEnumerable<IAllComponentRegistration> registrations)
    : ComponentRegistryBase<IAllComponentRegistry, IComponent>(registrations), IAllComponentRegistry
{
    public void RegisterComponent<T>() where T : struct, IComponent
        => RegisterComponentImpl(default(T));

    public void RegisterComponent(Type componentType)
        => RegisterComponentImpl(componentType);

    // FixMe(2026-10-18-twins-m6): records T only, so the managed mod side has no index table for it. The table is built
    // at bind from this registration.
    public void RegisterIndexedComponent<T, TKey>()
        where T : struct, IIndexedComponent<TKey>
        where TKey : unmanaged
        => RegisterComponent<T>();

    public void RegisterModComponent(ModComponentInfo info, string typeFullName)
        => throw new NotSupportedException(
            $"A mod component with a ready-made info only exists on the AOT side; {typeFullName} is registered by its type here.");
}
