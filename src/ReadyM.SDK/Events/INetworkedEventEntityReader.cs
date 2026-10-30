using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.SDK.Archetypes;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>Reads an event's entity members back off the wire. Used by generated code.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkedEventEntityReader
{
    /// <summary>Reads a required entity member and finds the entity it names.</summary>
    bool TryRead<T>(NetDataReader reader, out T shape, string networkEvent, string member)
        where T : struct, IArchetypeQueryable;

    /// <summary>Reads an optional entity member, which may have been sent as null.</summary>
    bool TryReadOptional<T>(NetDataReader reader, out T? shape, string networkEvent, string member)
        where T : struct, IArchetypeQueryable;
}
