using LiteNetLib.Utils;
using ReadyM.Api.DI;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Client;
using ReadyM.Api.Multiplayer.Protocol;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.SDK.Events;

/// <summary>
/// What a client's generated event dispatch derives from. The host fills it in at startup, the way it does an RPC
/// handler, so the seams generated code calls can reach the world and the relay through a typed <c>this</c>, and
/// the container starts it listening for its own event.
/// </summary>
/// <remarks>
/// Its members are internal on purpose: a mod never reads them, and the generated class that derives from this
/// never does either. Only the seams in this assembly do.
/// </remarks>
public abstract class NetworkedEventDispatchBase : NetworkedEventDispatchCore, IHostedService
{
    /// <summary>Where a raised event is sent, which is only ever the server. Set by the host.</summary>
    internal IRpcClient? Relay { get; set; }

    /// <summary>The guesses this client is holding, so a refusal can take the matching one back. Set by the host.</summary>
    internal NetworkedEventPredictions? Predictions { get; set; }

    private Action<ServerEventHeader, NetDataReader>? _listener;

    /// <summary>This machine, so a handler can be told whether it raised what just arrived.</summary>
    protected PlayerId Me => Relay?.PlayerId ?? PlayerId.Invalid;

    /// <summary>Generated: starts taking this event off the wire.</summary>
    protected abstract void Listen();

    /// <summary>Generated: the event's code, which is only known once the host has placed the mod's block.</summary>
    protected abstract RelayMessageCode Code();

    /// <summary>Takes the event's code, with the one delegate the relay removes by identity.</summary>
    protected void Hear(RelayMessageCode code, Action<ServerEventHeader, NetDataReader> arriving)
    {
        if (Relay is not { } relay)
            return;

        _listener = arriving;
        relay.AddServerRpcMessageHandler(code, arriving);
    }

    /// <summary>Generated code calls this when a refusal of this event arrives.</summary>
    protected void GuessFailed() => Predictions?.Refused(Code());

    public void OnScopeStart() => Listen();

    public virtual void Dispose()
    {
        if (Relay is { } relay && _listener is { } listener)
            relay.RemoveServerRpcMessageHandler(Code(), listener);

        _listener = null;
        GC.SuppressFinalize(this);
    }
}
