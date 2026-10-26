using System;
using System.ComponentModel;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Values;
using ReadyM.Api.Multiplayer.RPC;
using ReadyM.SDK.Archetypes;

namespace ReadyM.SDK.Entities;

/// <exclude />
/// <summary>
/// How generated RPC code sends a shape: as its entity's network id, looked up again where the handler runs. The
/// generator only lets shapes through that can be on the network.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class RpcEntityCodec
{
    private const byte Absent = 0;
    private const byte Present = 1;

    /// <exception cref="InvalidOperationException">There is no live networked entity behind the shape.</exception>
    public static void Write<T>(NetDataWriter writer, in T shape, string parameter) where T : struct, IArchetypeQueryable
    {
        writer.Put(Present);
        NetworkIdOf(shape, parameter).Serialize(writer);
    }

    /// <exception cref="InvalidOperationException">The shape is not null and there is no live networked entity behind it.</exception>
    public static void WriteOptional<T>(NetDataWriter writer, in T? shape, string parameter) where T : struct, IArchetypeQueryable
    {
        if (shape is not { } present)
        {
            writer.Put(Absent);
            return;
        }

        Write(writer, present, parameter);
    }

    /// <summary>Reads a required parameter and finds the entity it names, as <typeparamref name="T"/>.</summary>
    /// <returns>False, with a warning logged, when this end has no such entity or it is not a <typeparamref name="T"/>.</returns>
    public static bool TryRead<T>(RpcBase handlers, NetDataReader reader, out T shape, string handler, string parameter)
        where T : struct, IArchetypeQueryable
        => TryRead(World(handlers.RpcEntities), reader, out shape, handler, parameter);

    /// <summary>Reads an optional parameter and finds the entity it names, as <typeparamref name="T"/>, or null when it was sent as null.</summary>
    /// <returns>False, with a warning logged, when it names an entity this end does not have or that is not a <typeparamref name="T"/>.</returns>
    public static bool TryReadOptional<T>(RpcBase handlers, NetDataReader reader, out T? shape, string handler, string parameter)
        where T : struct, IArchetypeQueryable
        => TryReadOptional(World(handlers.RpcEntities), reader, out shape, handler, parameter);

    /// <summary>What both ends' reads come down to, once the world the handlers were given is in hand.</summary>
    internal static bool TryRead<T>(RpcEntities world, NetDataReader reader, out T shape, string handler, string parameter)
        where T : struct, IArchetypeQueryable
    {
        var id = ReadId(reader);

        if (id is { } named && TryFind(world, named, out shape))
            return true;

        Skipped<T>(world, id, handler, parameter);
        shape = default;
        return false;
    }

    /// <inheritdoc cref="TryRead{T}(RpcEntities, NetDataReader, out T, string, string)"/>
    internal static bool TryReadOptional<T>(RpcEntities world, NetDataReader reader, out T? shape, string handler, string parameter)
        where T : struct, IArchetypeQueryable
    {
        if (ReadId(reader) is not { } id)
        {
            shape = null;
            return true;
        }

        if (TryFind(world, id, out T found))
        {
            shape = found;
            return true;
        }

        Skipped<T>(world, id, handler, parameter);
        shape = null;
        return false;
    }

    /// <summary>The world a set of handlers was given, which the host does when it creates them.</summary>
    internal static RpcEntities World(object? rpcEntities)
        => rpcEntities as RpcEntities
           ?? throw new InvalidOperationException("These RPC handlers were not given a world to look entity parameters up in.");

    /// <summary>The network id a parameter was sent as, or null when it was sent as null.</summary>
    private static NetworkId? ReadId(NetDataReader reader)
    {
        if (reader.GetByte() == Absent)
            return null;

        var id = new NetworkId();
        id.Deserialize(reader);
        return id;
    }

    private static NetworkId NetworkIdOf<T>(in T shape, string parameter) where T : struct, IArchetypeQueryable
    {
        var handle = EntityHandle.Of(shape);

        if (!handle.IsAlive() || !handle.TryGetComponent(out MetadataComponent meta))
            throw new InvalidOperationException(
                $"RPC parameter '{parameter}' is a {typeof(T).Name} with no live networked entity behind it, so it cannot be sent.");

        return meta.NetId;
    }

    private static bool TryFind<T>(RpcEntities world, NetworkId id, out T shape) where T : struct, IArchetypeQueryable
    {
        var api = world.Api;

        if (api.TryFindByIndex<MetadataComponent, NetworkId>(id, out var raw))
            return new EntityHandle(raw, api).TryAs(out shape);

        shape = default;
        return false;
    }

    private static void Skipped<T>(RpcEntities world, NetworkId? id, string handler, string parameter)
        => world.Logger.LogWarning(
            "Skipped {Handler}: its {Parameter} names {Entity}, which this end does not have as a {Shape}.",
            handler, parameter, id is { } named ? $"entity {named}" : "no entity", typeof(T).Name);
}
