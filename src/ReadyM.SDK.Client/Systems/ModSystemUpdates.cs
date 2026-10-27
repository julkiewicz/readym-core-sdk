using Friflo.Engine.ECS.Systems;
using ReadyM.Api.DI;
using ReadyM.SDK.Events;
using ReadyM.SDK.Services;

namespace ReadyM.SDK.Client.Systems;

/// <summary>Wraps the [Service]s that declared an update into a Friflo system.</summary>
internal sealed class ModSystemUpdates : BaseSystem
{
    private readonly IDependencyContainer _services;

    private IReadOnlyList<IUpdatingService>? _updating;

    private ulong _count;

    private List<Events.ClientNetworkedEvents>? _events;

    private List<NetworkedEventPredictions>? _predictions;

    public ModSystemUpdates(IDependencyContainer services)
    {
        if (!ServiceRegistry.RegisteredFor(services))
        {
            throw new InvalidOperationException(
                "The services the mods declared are not registered on this container. Call "
                + nameof(DependencyInjectionExtensions.RegisterModDeclarations)
                + " once the mod assemblies are loaded and before their entry points run.");
        }

        _services = services;
    }

    public override string Name => "Mod systems";

    protected internal override void OnUpdateGroup()
    {
        if (!ModSystems.Running)
            return;

        _updating ??= ServiceRegistry.Resolve(_services);

        var moment = new UpdateTime(Tick.deltaTime, Tick.time, _count++);

        // Before the mods, so a guess that ran out is already gone by the time one of them reads anything.
        // Asked for as a set, because a host that wired no event raising has none and that is not an error.
        _predictions ??= _services.ResolveAll<NetworkedEventPredictions>().ToList();
        _events ??= _services.ResolveAll<INetworkedEvents>().OfType<Events.ClientNetworkedEvents>().ToList();

        foreach (var events in _events)
            events.Now = Tick.time;

        foreach (var predictions in _predictions)
            predictions.Expire(Tick.time);

        foreach (var service in _updating)
            service.Update(in moment);
    }
}