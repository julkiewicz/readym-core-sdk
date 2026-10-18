using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.ConflictResolution;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.Api.Multiplayer.ECS.Systems;

/// <summary>Sends one networked component's deltas: everything that does not need the component's type.</summary>
internal abstract class SendComponentDeltaSystemBase : QuerySystem<MetadataComponent>
{
    private readonly INetworkTime _networkTime;
    protected readonly NetworkedComponentId ComponentId;

    protected SendComponentDeltaSystemBase(INetworkTime networkTime, NetworkedComponentId componentId)
    {
        ComponentId = componentId;
        _networkTime = networkTime;
    }

    /// <returns>null if unbound, otherwise the max packet size in bytes</returns>
    protected abstract int? GetMaxPacketSize();

    protected abstract QueryFilter SetupFilter(QueryFilter filter, SendContext context);

    /// <summary>
    /// Returns a bitmask of player IDs to send to. Bit 0 = PlayerId 0, Bit 1 = PlayerId 1, etc.
    /// This is an optimization to avoid allocating HashSets every tick.
    /// </summary>
    protected abstract uint SentOwners();

    /// <summary>Sends a packet of an owner's deltas to everyone in scope EXCEPT that owner.</summary>
    protected abstract void SendExceptOwner(PlayerId owner, NetDataWriter data, SendContext context);

    /// <summary>
    /// When true, components touched by the API (ChangedFromApi) additionally have their delta
    /// sent to the entity's own owner, so the server can authoritatively override the owner's state.
    /// An entity the server owns itself has no remote owner to override, so it never gets that copy.
    /// Defaults to false; the server overrides it.
    /// </summary>
    protected virtual bool SendApiDeltasToOwner => false;

    /// <summary>
    /// Owner-directed send used for API-authored deltas. Only invoked when SendApiDeltasToOwner is true.
    /// </summary>
    protected virtual void SendToOwner(PlayerId owner, NetDataWriter data, SendContext context) { }

    /// <summary>
    /// Appends the owner's dirty deltas in the scope to <paramref name="others"/>, and the API-authored ones to
    /// <paramref name="toOwner"/> too. Returns false when the next delta would not fit under
    /// <paramref name="maxPacketSize"/>; called again with fresh packets, it continues from there.
    /// </summary>
    protected abstract bool WriteDeltas(
        PlayerId owner,
        SendContext context,
        NetDataWriter others,
        NetDataWriter? toOwner,
        int? maxPacketSize);

    private void CreatePacketHeader(NetDataWriter writer)
    {
        writer.Put((byte)RelayMessageCode.EcsDelta);
        writer.Put(_networkTime.GetCurrentTime());
        writer.Put(ComponentId);
    }

    protected override void OnUpdate()
        => OnUpdate(default);

    [ThreadStatic]
    private static NetDataWriter? _othersWriter;

    [ThreadStatic]
    private static NetDataWriter? _ownerWriter;

    protected void OnUpdate(SendContext context)
    {
        var maxPacketSize = GetMaxPacketSize();
        var owners = SentOwners();

        for (var ownerIx = 0; ownerIx < 32; ownerIx++)
        {
            if ((owners & (1u << ownerIx)) == 0)
                continue;

            SendForOwner(new PlayerId((ushort)ownerIx), maxPacketSize, context);
        }
    }

    private void SendForOwner(PlayerId owner, int? maxPacketSize, SendContext context)
    {
        // "owner" packet: API-authored deltas sent back to the owner.
        var sendApiDeltasToOwner = SendApiDeltasToOwner && owner != PlayerId.Server;

        bool done;
        do
        {
            // "others" packet: this owner's deltas relayed to everyone else in scope (the normal path).
            var others = ResetWriter(ref _othersWriter);
            var othersHeaderSize = others.Length;

            var owned = sendApiDeltasToOwner ? ResetWriter(ref _ownerWriter) : null;
            var ownedHeaderSize = owned?.Length ?? 0;

            done = WriteDeltas(owner, context, others, owned, maxPacketSize);

            if (others.Length > othersHeaderSize)
                SendExceptOwner(owner, others, context);

            if (owned != null && owned.Length > ownedHeaderSize)
                SendToOwner(owner, owned, context);
        }
        while (!done);
    }

    private NetDataWriter ResetWriter(ref NetDataWriter? slot)
    {
        slot ??= new NetDataWriter();
        slot.Reset();
        CreatePacketHeader(slot);
        return slot;
    }
}

/// <summary>Sends one native networked component's deltas: what needs the component's type.</summary>
internal abstract class SendComponentDeltaSystemBase<T> : SendComponentDeltaSystemBase
    where T : struct, INetworkedComponent
{
    private readonly bool _clearFlags;
    private readonly QueryCacheHelper<SendContext, Entity?, ArchetypeQuery<MetadataComponent, T>> _queryCache;

    // Dirty entities of the owner to skip, when the last call stopped at the packet size cap
    private int _resumeAt;

    protected SendComponentDeltaSystemBase(INetworkTime networkTime, NetworkedComponentId componentId, bool clearFlags)
        : base(networkTime, componentId)
    {
        Filter.AllComponents(ComponentTypes.Get<T>());
        _clearFlags = clearFlags;
        _queryCache = new QueryCacheHelper<SendContext, Entity?, ArchetypeQuery<MetadataComponent, T>>(
            context => context.ScopeEntity,
            context =>
            {
                var filter = new QueryFilter();
                filter = SetupFilter(filter, context);
                var query = Query.Store.Query<MetadataComponent, T>(filter);
                return query;
            }
        );
    }

    protected sealed override bool WriteDeltas(
        PlayerId owner,
        SendContext context,
        NetDataWriter others,
        NetDataWriter? toOwner,
        int? maxPacketSize)
    {
        var seen = 0;
        var wroteAny = false;

        foreach (var (metaChunk, compChunk, _) in _queryCache.GetQuery(context).Chunks)
        {
            var metaSpan = metaChunk.Span;
            var compSpan = compChunk.Span;

            for (var i = 0; i < metaChunk.Length; i++)
            {
                var meta = metaSpan[i];
                ref var comp = ref compSpan[i];

                if (meta.Owner != owner)
                    continue;

                if (!comp.IsDirty)
                    continue;

                if (seen++ < _resumeAt)
                    continue;

                var othersStart = others.Length;
                var toOwnerStart = toOwner?.Length ?? 0;

                // API-authored deltas additionally go back to the owner (single scan, no second pass).
                var alsoToOwner = toOwner != null && comp.ChangedFromApi;
                if (alsoToOwner)
                {
                    toOwner!.Put(meta.NetId);
                    comp.WriteDelta(toOwner);
                }

                others.Put(meta.NetId);
                comp.WriteDelta(others);

                if (maxPacketSize is { } cap && (others.Length > cap || (alsoToOwner && toOwner!.Length > cap)))
                {
                    if (!wroteAny)
                        throw new InvalidOperationException($"Component {typeof(T).Name} with NetId {meta.NetId} is too large to fit in a packet even by itself. Max packet size is {cap} bytes.");

                    others.SetPosition(othersStart);
                    toOwner?.SetPosition(toOwnerStart);

                    // Once their flags are cleared, the entities already written are no longer dirty
                    _resumeAt = _clearFlags ? 0 : seen - 1;
                    return false;
                }

                wroteAny = true;

                if (_clearFlags)
                {
                    comp.ClearDirty();
                    comp.ClearApiFlag();
                }
            }
        }

        _resumeAt = 0;
        return true;
    }
}
