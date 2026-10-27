using LiteNetLib.Utils;
using ReadyM.Api.DI;
using ReadyM.Api.Multiplayer.Protocol;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.Relay.Server.Sdk.Players;
using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Entities;

namespace ReadyM.SDK.Server.Events;

/// <summary>
/// What a server mod's generated event dispatch derives from. The host fills it in at startup, the way it does a
/// server RPC handler, so the seams generated code calls can reach the world and the relay through a typed
/// <c>this</c>, and the container starts it listening for its own event.
/// </summary>
/// <remarks>
/// Its members are internal on purpose: a mod never reads them, and the generated class that derives from this
/// never does either. Only the seams in this assembly do.
/// </remarks>
public abstract class ServerNetworkedEventDispatchBase : NetworkedEventDispatchCore, IHostedService
{
    /// <summary>Where a global event's recipients are found. Set by the host.</summary>
    internal IEntities? Lookup { get; set; }

    /// <summary>
    /// Who is in an area or a cell. The scope tree holds the entities a scope holds and a player is not one of
    /// them: a player entity lives in the server scope and the relay tracks where it is. Set by the host.
    /// </summary>
    internal IPlayers? Players { get; set; }

    /// <summary>Where an accepted event is fanned out and a refusal is replied. Set by the host.</summary>
    internal RpcApi? Relay { get; set; }

    private Action<ServerEventHeader, NetDataReader>? _listener;

    /// <summary>Generated: starts taking this event off the wire.</summary>
    protected abstract void Listen();

    /// <summary>Takes the event's code, with the one delegate the relay removes by identity.</summary>
    protected void Hear(RelayMessageCode code, Action<ServerEventHeader, NetDataReader> arriving)
    {
        if (Relay is not { } relay)
            return;

        _listener = arriving;
        relay.AddServerRpcMessageHandler(code, arriving);
    }

    public void OnScopeStart() => Listen();

    public virtual void Dispose()
    {
        if (Relay is { } relay && _listener is { } listener)
            relay.RemoveServerRpcMessageHandler(Code(), listener);

        _listener = null;
        GC.SuppressFinalize(this);
    }

    /// <summary>Generated: the event's code, which is only known once the host has placed the mod's block.</summary>
    protected abstract RelayMessageCode Code();
}
