using LiteNetLib;
using ReadyM.Api.DI;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Tests.Client.Fixtures;

namespace ReadyM.SDK.Tests.Archetypes;

/// <summary>
/// The test assembly's generated helpers, called the way a loader calls them: every component its shapes bring on the
/// "all" twin once, every replicated one on the networked twin once, with its delivery.
/// </summary>
public sealed class GeneratedRegistrationTests : IDisposable
{
    private sealed class Container : DependencyContainerBase;

    private readonly Container _services = new();
    private readonly ModAllComponentRegistry _all;
    private readonly ModNetworkedComponentRegistry _networked;

    public GeneratedRegistrationTests()
    {
        _services.Init();

        Assert.True(GeneratedShapes.Register(_services, typeof(Station).Assembly));

        // Built from the registrations, so registering one component twice would throw here
        _all = new ModAllComponentRegistry(_services.ResolveAll<IAllComponentRegistration>());
        _networked = new ModNetworkedComponentRegistry(_services.ResolveAll<INetworkedComponentRegistration>());
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public void Every_component_a_shape_brings_is_on_the_all_twin()
    {
        Assert.Contains(typeof(ClimateComponent), _all.ComponentTypes);
        Assert.Contains(typeof(StationComponent), _all.ComponentTypes);
        Assert.Contains(typeof(TelemetryComponent), _all.ComponentTypes);
        Assert.Contains(typeof(MotionComponent), _all.ComponentTypes);
        Assert.Contains(_all.ComponentTypes, component => component.Name == "StationArchetypeMarker");
    }

    [Fact]
    public void Every_replicated_component_is_on_the_networked_twin_with_its_delivery()
    {
        Assert.Equal(DeliveryMethod.ReliableOrdered, _networked.GetNetworkedComponentDeliveryMethod<TelemetryComponent>());
        Assert.Equal(DeliveryMethod.Unreliable, _networked.GetNetworkedComponentDeliveryMethod<MotionComponent>());
    }

    [Fact]
    public void A_local_shape_puts_nothing_on_the_networked_twin()
    {
        Assert.DoesNotContain(typeof(ClimateComponent), _networked.ComponentTypes);
        Assert.DoesNotContain(typeof(StationComponent), _networked.ComponentTypes);
    }

    [Fact]
    public void An_assembly_without_shapes_registers_nothing()
    {
        using var empty = new Container();
        empty.Init();

        Assert.False(GeneratedShapes.Register(empty, typeof(object).Assembly));
        Assert.Empty(empty.ResolveAll<IAllComponentRegistration>());
    }
}
