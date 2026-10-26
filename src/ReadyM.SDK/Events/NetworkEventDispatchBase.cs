using ReadyM.Api.Multiplayer.Client;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <summary>
/// What a client's generated event dispatch derives from. The host fills it in at startup, the way it does an RPC
/// handler, so the seams generated code calls can reach the world and the relay through a typed <c>this</c>.
/// </summary>
/// <remarks>
/// Its members are internal on purpose: a mod never reads them, and the generated class that derives from this
/// never does either. Only the seams in this assembly do.
/// </remarks>
public abstract class NetworkEventDispatchBase
{
    /// <summary>Where entity members are looked up, and where a skipped handler is logged. Set by the host.</summary>
    internal RpcEntities? Entities { get; set; }

    /// <summary>Where a raised event is sent, which is only ever the server. Set by the host.</summary>
    internal IRpcClient? Relay { get; set; }
}
