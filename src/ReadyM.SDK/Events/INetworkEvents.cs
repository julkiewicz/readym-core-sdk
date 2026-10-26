using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <summary>Raises network events. Take it as a constructor parameter, like any other service.</summary>
public interface INetworkEvents
{
    /// <summary>Raises the event. The origin is this client, or the server.</summary>
    /// <returns>A handle a client may hang a guess on while it waits for the answer.</returns>
    INetworkEventRaise Raise<T>(T networkEvent) where T : struct, INetworkEvent;

    /// <summary>
    /// Server only: raises it as though <paramref name="origin"/> did, so handlers cannot tell it from that
    /// player's own event. The policy check is skipped, because the server vouched for it.
    /// </summary>
    void RaiseAs<T>(PlayerId origin, T networkEvent) where T : struct, INetworkEvent;
}
