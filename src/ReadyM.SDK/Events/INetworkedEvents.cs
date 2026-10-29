using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <summary>Allows raising networked events.</summary>
public interface INetworkedEvents
{
    /// <summary>Raises the event. The origin is this client, or the server.</summary>
    /// <returns>A handle a client may use to provide prediction and rollback logic while it waits for the answer.</returns>
    INetworkedEventRaise Raise<T>(T networkEvent) where T : struct, INetworkedEvent;

    /// <summary>
    /// Server only: raises it as though <paramref name="origin"/> did, so handlers cannot tell it from that
    /// player's own event. The policy check is skipped, because the server vouched for it.
    /// </summary>
    /// <remarks>
    /// Useful for implementing server-side bots or AI that act on behalf of a player,
    /// or for a server-side mod that wants to raise an event as though it came from a player.
    /// </remarks>
    void RaiseAs<T>(PlayerId origin, T networkEvent) where T : struct, INetworkedEvent;
}
