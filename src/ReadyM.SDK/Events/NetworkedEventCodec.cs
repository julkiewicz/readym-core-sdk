using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// Reads an event's entity members back off the wire. Used by generated code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkedEventCodec
{
    /// <summary>Reads a required entity member and finds what it names.</summary>
    public static bool TryRead<T>(
        NetworkedEventDispatchCore dispatch,
        NetDataReader reader,
        out T shape,
        string networkEvent,
        string member
    ) where T : struct, IArchetypeQueryable
    {
        if (dispatch.Entities is { } world)
            return RpcEntityCodec.TryRead(world, reader, out shape, networkEvent, member);

        shape = default;
        return false;
    }

    /// <summary>Reads an optional entity member, which may have been sent as null.</summary>
    public static bool TryReadOptional<T>(
        NetworkedEventDispatchCore dispatch, NetDataReader reader, out T? shape, string networkEvent, string member)
        where T : struct, IArchetypeQueryable
    {
        if (dispatch.Entities is { } world)
            return RpcEntityCodec.TryReadOptional(world, reader, out shape, networkEvent, member);

        shape = null;
        return false;
    }
}