using System.ComponentModel;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>One mod's generated handlers for one event.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkedEventHandlers<T> where T : struct, INetworkedEvent
{
    /// <summary>The unique ID of the mod these handlers belong to.</summary>
    string Mod { get; }

    /// <summary>The mods whose handlers run before these.</summary>
    IReadOnlyList<string> After { get; }

    /// <summary>Runs this mod's handlers for one stage, in the order the mod declared them.</summary>
    void Run(Stage stage, ref T networkEvent, EventContext context);
}
