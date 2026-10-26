using ReadyM.Relay.Server.Sdk.Rpc;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Server.Events;

/// <summary>
/// What a server mod's generated event dispatch derives from. The host fills it in at startup, the way it does a
/// server RPC handler, so the seams generated code calls can reach the world and the relay through a typed
/// <c>this</c>.
/// </summary>
/// <remarks>
/// Its members are internal on purpose: a mod never reads them, and the generated class that derives from this
/// never does either. Only the seams in this assembly do.
/// </remarks>
public abstract class ServerNetworkEventDispatchBase
{
    /// <summary>Where entity members are looked up, and where a skipped handler is logged. Set by the host.</summary>
    internal RpcEntities? Entities { get; set; }

    /// <summary>Where an accepted event is fanned out and a refusal is replied. Set by the host.</summary>
    internal RpcApi? Relay { get; set; }
}
