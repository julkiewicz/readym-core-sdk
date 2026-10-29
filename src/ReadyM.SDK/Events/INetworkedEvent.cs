using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

/// <summary>
/// Added to a struct marked <see cref="Attributes.NetworkedEventAttribute"/> by the source generator.
/// </summary>
/// <exclude />
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkedEvent
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    RelayMessageCode Code { get; }

    [EditorBrowsable(EditorBrowsableState.Never)]
    void Serialize(NetDataWriter writer);
}
