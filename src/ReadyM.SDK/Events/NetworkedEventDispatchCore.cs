using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <summary>
/// What both sides' generated event dispatch has in common: the world its members are looked up in. A mod never
/// touches this, and neither does the generated class that derives from it. Only the seams in the SDK do.
/// </summary>
public abstract class NetworkedEventDispatchCore
{
    /// <summary>Where entity members are looked up, and where a skipped handler is logged. Set by the host.</summary>
    internal RpcEntities? Entities { get; set; }
}
