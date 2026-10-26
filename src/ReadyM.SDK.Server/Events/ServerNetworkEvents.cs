using ReadyM.Api.DI;
using ReadyM.Api.Idents;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Server.Events;

/// <summary>
/// Raising an event from server code. The stages run here and now rather than going anywhere first, because the
/// server is where they would have run anyway.
/// </summary>
internal sealed class ServerNetworkEvents(IDependencyContainer container) : INetworkEvents
{
    public INetworkEventRaise Raise<T>(T networkEvent) where T : struct, INetworkEvent
    {
        Dispatch<T>().Raise(networkEvent, PlayerId.Server, true);

        return ServerRaise.Instance;
    }

    public void RaiseAs<T>(PlayerId origin, T networkEvent) where T : struct, INetworkEvent
        => Dispatch<T>().Raise(networkEvent, origin, true);

    private INetworkEventDispatch<T> Dispatch<T>() where T : struct, INetworkEvent
        => container.Resolve<INetworkEventDispatch<T>>();

    /// <summary>
    /// The server already knows the answer, because it is the one giving it. A guess here would be a mod
    /// written for a client running where it cannot work, so it says so rather than quietly doing nothing.
    /// </summary>
    private sealed class ServerRaise : INetworkEventRaise
    {
        public static ServerRaise Instance { get; } = new();

        public INetworkEventRaise Predicting(Action guess)
            => throw new InvalidOperationException(
                "Only a client predicts. The server runs the event's stages itself, so there is nothing to guess.");

        public INetworkEventRaise Rollback(Action undo) => Predicting(undo);
    }
}
