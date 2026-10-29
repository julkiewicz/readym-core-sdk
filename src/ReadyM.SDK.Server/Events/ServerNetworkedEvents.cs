using ReadyM.Api.DI;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Server.Entities;
using ReadyM.Api.Idents;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Server.Events;

/// <summary>
/// Raising an event from server code. The stages run here and now rather than going anywhere first, because the
/// server is where they would have run anyway.
/// </summary>
internal sealed class ServerNetworkedEvents(IDependencyContainer container) : INetworkedEvents
{
    public INetworkedEventRaise Raise<T>(T networkEvent) where T : struct, INetworkedEvent
    {
        NetworkedEventThread.ThrowIfNotOnWorldThread<T>(OnWorldThread());
        Dispatch<T>().Raise(networkEvent, PlayerId.Server, true);

        return ServerRaise.Instance;
    }

    public void RaiseAs<T>(PlayerId origin, T networkEvent) where T : struct, INetworkedEvent
    {
        NetworkedEventThread.ThrowIfNotOnWorldThread<T>(OnWorldThread());
        Dispatch<T>().Raise(networkEvent, origin, true);
    }

    /// <summary>
    /// Asked of the host through the entity api, since a mod runs on the far side of the host's boundary and has
    /// no world of its own to ask.
    /// </summary>
    private bool OnWorldThread()
        => container.Resolve<IEntityApi>() is ServerEntityApi api
            ? api.IsOnWorldThread
            : throw new InvalidOperationException(
                $"The server's entity api is {container.Resolve<IEntityApi>().GetType().Name}, which cannot say "
                + "which thread the world runs on, so whether this raise is on it cannot be checked.");

    private INetworkedEventDispatch<T> Dispatch<T>() where T : struct, INetworkedEvent
        => container.Resolve<INetworkedEventDispatch<T>>();

    /// <summary>
    /// The server already knows the answer, because it is the one giving it. A guess here would be a mod
    /// written for a client running where it cannot work, so it says so rather than quietly doing nothing.
    /// </summary>
    private sealed class ServerRaise : INetworkedEventRaise
    {
        public static ServerRaise Instance { get; } = new();

        public INetworkedEventRaise Predicting(Action prediction)
            => throw new InvalidOperationException(
                "Only a client predicts. The server runs the event's stages itself, so there is nothing to guess.");

        public INetworkedEventRaise Rollback(Action rollback) => Predicting(rollback);
    }
}
