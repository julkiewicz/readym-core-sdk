using LiteNetLib;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Tests.Client.Fixtures;

namespace ReadyM.SDK.Tests.Server;

/// <summary>
/// The 1.0 shapes' components on the managed mod side's twins: each replicated one once on each twin, the rest of a
/// shape's own components on the "all" twin only.
/// </summary>
public class ShapeComponentRegistrationTests
{
    private readonly ModAllComponentRegistry _all;
    private readonly ModNetworkedComponentRegistry _networked;

    public ShapeComponentRegistrationTests()
    {
        global::ReadyM.SDK.Generated.ShapeRegistrations.RegisterAll();

        _all = new ModAllComponentRegistry([new ShapeComponentRegistration()]);
        _networked = new ModNetworkedComponentRegistry([new ReplicatedShapeRegistration()]);
    }

    [Fact]
    public void Each_replicated_shape_component_is_on_each_twin_once()
    {
        var replicated = ReplicationRegistry.InWireOrder().Select(r => r.Component).ToList();

        Assert.Contains(typeof(TelemetryComponent), replicated);
        Assert.Equal(replicated, _networked.ComponentTypes);
        Assert.All(replicated, component => Assert.Single(_all.ComponentTypes, component));
    }

    [Fact]
    public void A_replicated_shape_component_keeps_its_delivery_method()
    {
        Assert.Equal(DeliveryMethod.Unreliable, _networked.GetNetworkedComponentDeliveryMethod<MotionComponent>());
        Assert.Equal(DeliveryMethod.ReliableOrdered, _networked.GetNetworkedComponentDeliveryMethod<TelemetryComponent>());
    }

    [Fact]
    public void The_other_own_components_of_a_shape_are_on_the_all_twin_only_once()
    {
        var local = ArchetypeContributions.Shapes()
            .SelectMany(ArchetypeContributions.OwnGenerated)
            .Where(component => !typeof(INetworkedComponent).IsAssignableFrom(component))
            .Distinct()
            .ToList();

        Assert.NotEmpty(local);
        Assert.All(local, component => Assert.Single(_all.ComponentTypes, component));
        Assert.DoesNotContain(_networked.ComponentTypes, local.Contains);
    }

    [Fact]
    public void The_all_twin_holds_nothing_twice()
    {
        Assert.Equal(_all.ComponentTypes.Distinct(), _all.ComponentTypes);
    }
}
