using System.ComponentModel;

namespace ReadyM.SDK.Events;

/// <summary>
/// What the generator adds to a struct marked <see cref="Attributes.NetworkEventAttribute"/>. A mod never writes
/// it.
/// </summary>
/// <exclude />
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkEvent;
