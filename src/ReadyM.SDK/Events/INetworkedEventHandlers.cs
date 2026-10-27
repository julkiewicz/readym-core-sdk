using System.Collections.Generic;
using System.ComponentModel;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// One mod's handlers for one event, as the coordinator that runs them sees it. A mod generates one of these
/// per event it handles; the coordinator holds every mod's, so it can finish a stage everywhere before
/// starting the next one.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkedEventHandlers<T> where T : struct, INetworkedEvent
{
    /// <summary>The mod these belong to, as a refusal names it and as another mod's After names it.</summary>
    string Mod { get; }

    /// <summary>The mods whose handlers this one's run behind.</summary>
    IReadOnlyList<string> After { get; }

    /// <summary>Runs this mod's handlers for one stage, in the order the mod declared them.</summary>
    void Run(Stage stage, ref T networkEvent, EventContext context);
}
