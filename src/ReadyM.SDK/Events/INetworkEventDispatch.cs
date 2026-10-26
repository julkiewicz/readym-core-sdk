using System.ComponentModel;
using ReadyM.Api.Idents;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// One mod's generated dispatch for one event, as the thing that raises it sees it. A mod raises through
/// <see cref="INetworkEvents"/> and never names this: it exists so a raise can find the dispatch for the event's
/// type without the SDK knowing any mod's generated class.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkEventDispatch<T> where T : struct, INetworkEvent
{
    /// <summary>
    /// Raises it. A client sends it to the server and runs nothing locally; the server runs the stages here and
    /// fans out what survives them.
    /// </summary>
    void Raise(T networkEvent, PlayerId origin);
}
