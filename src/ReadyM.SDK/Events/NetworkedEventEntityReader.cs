using LiteNetLib.Utils;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

internal sealed class NetworkedEventEntityReader(RpcEntities entities) : INetworkedEventEntityReader
{
    public bool TryRead<T>(NetDataReader reader, out T shape, string networkEvent, string member)
        where T : struct, IArchetypeQueryable
        => RpcEntityCodec.TryRead(entities, reader, out shape, networkEvent, member);

    public bool TryReadOptional<T>(NetDataReader reader, out T? shape, string networkEvent, string member)
        where T : struct, IArchetypeQueryable
        => RpcEntityCodec.TryReadOptional(entities, reader, out shape, networkEvent, member);
}
