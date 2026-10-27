using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// How generated dispatch reads an event's entity members back off the wire. An id names an entity this end may
/// not have, so every read is try-shaped and a miss drops the whole event rather than handing a handler a
/// default shape that looks like a real one.
/// </summary>
/// <remarks>
/// Reading needs the world and writing does not, which is why an event serializes itself and is read here. Both
/// sides go through the same seam because both hold the same kind of world.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class NetworkEventCodec
{
    /// <summary>Reads a required entity member and finds what it names.</summary>
    public static bool TryRead<T>(
        NetworkEventDispatchCore dispatch, NetDataReader reader, out T shape, string networkEvent, string member)
        where T : struct, IArchetypeQueryable
    {
        if (dispatch.Entities is { } world)
            return RpcEntityCodec.TryRead(world, reader, out shape, networkEvent, member);

        shape = default;
        return false;
    }

    /// <summary>Reads an optional entity member, which may have been sent as null.</summary>
    public static bool TryReadOptional<T>(
        NetworkEventDispatchCore dispatch, NetDataReader reader, out T? shape, string networkEvent, string member)
        where T : struct, IArchetypeQueryable
    {
        if (dispatch.Entities is { } world)
            return RpcEntityCodec.TryReadOptional(world, reader, out shape, networkEvent, member);

        shape = default;
        return false;
    }
}
