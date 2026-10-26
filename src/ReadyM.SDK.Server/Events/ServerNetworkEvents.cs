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
    public void Raise<T>(T networkEvent) where T : struct, INetworkEvent
        => Dispatch<T>().Raise(networkEvent, PlayerId.Server);

    public void RaiseAs<T>(PlayerId origin, T networkEvent) where T : struct, INetworkEvent
        => Dispatch<T>().Raise(networkEvent, origin);

    private INetworkEventDispatch<T> Dispatch<T>() where T : struct, INetworkEvent
        => container.Resolve<INetworkEventDispatch<T>>();
}
