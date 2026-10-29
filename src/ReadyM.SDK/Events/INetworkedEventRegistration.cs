using ReadyM.Api.DI;

namespace ReadyM.SDK.Events;

/// <summary>
/// How a side registers its own coordinator in the container. The registry holds contracts without knowing which
/// machine it is on, and a coordinator is the one piece that differs between the two: the server checks the
/// policy and fans out, the client only receives.
/// </summary>
internal interface INetworkedEventRegistration
{
    /// <summary>Registers this side's coordinator for one event, which is also what starts it listening.</summary>
    void Register<T>(IDependencyContainer container, INetworkedEventContract<T> contract)
        where T : struct, INetworkedEvent;
}
