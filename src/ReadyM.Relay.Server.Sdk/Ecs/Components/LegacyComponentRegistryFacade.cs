using LiteNetLib;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;

namespace ReadyM.Relay.Server.Sdk.Ecs.Components;

// Implements the obsolete 0.x interface on purpose
#pragma warning disable CS0618

/// <summary>
/// The 0.x surface: records what a ServerModBase declares and replays it on both twins when they are built.
/// Recording only, so declaring does no work and the order of declaration is the only order that survives.
/// </summary>
internal sealed class LegacyComponentRegistryFacade : IComponentRegistry, IAllComponentRegistration, INetworkedComponentRegistration
{
    private readonly HashSet<Type> _declared = [];
    private readonly List<Action<IAllComponentRegistry>> _allComponentRegistrations = [];
    private readonly List<Action<INetworkedComponentRegistry>> _networkedComponentRegistrations = [];

    public void RegisterLocalComponent<T>() where T : struct
        => RegisterLocalComponent(typeof(T));

    public void RegisterLocalComponent(Type component)
    {
        AddDeclaredComponent(component);

        // By type: a 0.x local component carries no IComponent constraint
        _allComponentRegistrations.Add(registry => registry.RegisterComponent(component));
    }

    public void RegisterComponent<T>(byte delivery = 0) where T : struct, INetworkedComponent
    {
        AddDeclaredComponent(typeof(T));
        _allComponentRegistrations.Add(registry => registry.RegisterComponent<T>());
        _networkedComponentRegistrations.Add(registry => registry.RegisterComponent<T>(ToDeliveryMethod(delivery)));
    }

    public void RegisterComponent(Type component, byte delivery)
    {
        AddDeclaredComponent(component);
        _allComponentRegistrations.Add(registry => registry.RegisterComponent(component));
        _networkedComponentRegistrations.Add(registry => registry.RegisterComponent(component, ToDeliveryMethod(delivery)));
    }

    void IComponentRegistrationBase<IAllComponentRegistry, Friflo.Engine.ECS.IComponent>.Register(IAllComponentRegistry registry)
    {
        foreach (var register in _allComponentRegistrations)
            register(registry);
    }

    void IComponentRegistrationBase<INetworkedComponentRegistry, INetworkedComponent>.Register(INetworkedComponentRegistry registry)
    {
        foreach (var register in _networkedComponentRegistrations)
            register(registry);
    }

    // Throws on a second declaration, so that mod is skipped while declaring
    private void AddDeclaredComponent(Type component)
    {
        if (!component.IsValueType)
            throw new ArgumentException($"{component.FullName} is not a value type.", nameof(component));

        if (!_declared.Add(component))
            throw new InvalidOperationException($"{component.FullName} is already registered.");
    }

    // 0 is reliable, 1 unreliable; reliable means ordered as well
    private static DeliveryMethod ToDeliveryMethod(byte delivery)
        => delivery == 0 ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
}
