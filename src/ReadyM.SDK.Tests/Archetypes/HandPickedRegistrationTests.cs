using Friflo.Engine.ECS;
using LiteNetLib;
using ReadyM.Api.DI;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Tests.Client.Fixtures;

namespace ReadyM.SDK.Tests.Archetypes;

/// <summary>
/// Ordinary tests over the fixture shapes, compiled as usual: each picks the generated registrations it wants into a
/// container of its own. If any of it went back into a static, one test's picks would show up in another's.
/// </summary>
public sealed class HandPickedRegistrationTests : IDisposable
{
    private sealed class Container : DependencyContainerBase;

    // Records which component an indexed shape names, instead of finding anything
    private struct RecordingFinder<TKey> : IIndexFinder<TKey>
    {
        public Type? Component;

        public bool TryFind<TComponent>(TKey key, out RawEntity entity)
            where TComponent : struct, IIndexedComponent<TKey>
        {
            Component = typeof(TComponent);
            entity = default;
            return false;
        }
    }

    private readonly Container _services = new();

    public HandPickedRegistrationTests() => _services.Init();

    public void Dispose() => _services.Dispose();

    private static ModAllComponentRegistry AllTwinOf(IDependencyContainer services)
        => new(services.ResolveAll<IAllComponentRegistration>());

    private static ModNetworkedComponentRegistry NetworkedTwinOf(IDependencyContainer services)
        => new(services.ResolveAll<INetworkedComponentRegistration>());

    [Fact]
    public void A_test_sees_exactly_the_components_it_registered()
    {
        ClimateComponentRegistrationHelper.Register(_services);

        Assert.Equal([typeof(ClimateComponent)], AllTwinOf(_services).ComponentTypes);
        Assert.Empty(NetworkedTwinOf(_services).ComponentTypes);
    }

    [Fact]
    public void A_test_sees_exactly_the_replication_it_registered()
    {
        MotionReplication.Register(_services);

        var networked = NetworkedTwinOf(_services);

        Assert.Equal([typeof(MotionComponent)], networked.ComponentTypes);
        Assert.Equal(DeliveryMethod.Unreliable, networked.GetNetworkedComponentDeliveryMethod<MotionComponent>());
        Assert.Empty(AllTwinOf(_services).ComponentTypes);
    }

    [Fact]
    public void Two_containers_in_one_process_do_not_share_what_they_registered()
    {
        using var other = new Container();
        other.Init();

        TelemetryReplication.Register(_services);
        MotionReplication.Register(other);

        Assert.Equal([typeof(TelemetryComponent)], NetworkedTwinOf(_services).ComponentTypes);
        Assert.Equal([typeof(MotionComponent)], NetworkedTwinOf(other).ComponentTypes);
    }

    [Fact]
    public void An_indexed_shape_names_its_component_with_nothing_registered()
        => Assert.Equal(typeof(StationComponent), ComponentBehind<Station>());

    // On the shape's own struct type, as TryLookup calls it
    private static Type? ComponentBehind<T>() where T : struct, IIndexed<int>
    {
        var finder = new RecordingFinder<int>();
        default(T).TryFind(ref finder, 7, out _);
        return finder.Component;
    }
}
