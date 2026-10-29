using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <summary>
/// One mod's generated dispatch for one event. A mod raises through
/// <see cref="INetworkedEvents"/> and never names this: it exists so a raise can find the dispatch for the event's
/// type without the SDK knowing any mod's generated class.
/// </summary>
internal interface INetworkedEventDispatch<in T> where T : struct, INetworkedEvent
{
    /// <param name="vouched">
    /// The server is raising it for a player rather than taking it off the wire, so the policy is not asked
    /// again. That is what lets a bot act like a player.
    /// </param>
    void Raise(T networkEvent, PlayerId origin, bool vouched);
}
