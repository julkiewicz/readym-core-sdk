using System.ComponentModel;
using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// One mod's generated dispatch for one event, as the thing that raises it sees it. A mod raises through
/// <see cref="INetworkedEvents"/> and never names this: it exists so a raise can find the dispatch for the event's
/// type without the SDK knowing any mod's generated class.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkedEventDispatch<T> where T : struct, INetworkedEvent
{
    /// <summary>
    /// Raises it. A client sends it to the server and runs nothing locally; the server runs the stages here and
    /// fans out what survives them.
    /// </summary>
    /// <param name="vouched">
    /// The server is raising it for a player rather than taking it off the wire, so the policy is not asked
    /// again. That is what lets a bot act like a player.
    /// </param>
    void Raise(T networkEvent, PlayerId origin, bool vouched);
}
