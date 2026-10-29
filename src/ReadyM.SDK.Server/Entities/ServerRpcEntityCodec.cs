using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Server.Entities;

/// <exclude />
/// <summary>
/// How generated server RPC code reads a shape parameter. <see cref="RpcEntityCodec"/> does the rest; its reads take the
/// client's handler base, which the server's is not.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServerRpcEntityCodec
{
    /// <inheritdoc cref="RpcEntityCodec.TryRead{T}(ReadyM.Api.Multiplayer.RPC.RpcBase, NetDataReader, out T, string, string)"/>
    public static bool TryRead<T>(ServerRpcHandlersBase handlers, NetDataReader reader, out T shape, string handler, string parameter)
        where T : struct, IArchetypeQueryable
        => RpcEntityCodec.TryRead(RpcEntityCodec.World(handlers.RpcEntities), reader, out shape, handler, parameter);

    /// <inheritdoc cref="RpcEntityCodec.TryReadOptional{T}(ReadyM.Api.Multiplayer.RPC.RpcBase, NetDataReader, out T?, string, string)"/>
    public static bool TryReadOptional<T>(ServerRpcHandlersBase handlers, NetDataReader reader, out T? shape, string handler, string parameter)
        where T : struct, IArchetypeQueryable
        => RpcEntityCodec.TryReadOptional(RpcEntityCodec.World(handlers.RpcEntities), reader, out shape, handler, parameter);
}
