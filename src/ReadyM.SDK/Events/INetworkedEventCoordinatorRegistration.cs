using ReadyM.Api.DI;

namespace ReadyM.SDK.Events;

/// <summary>Registers one side's coordinator for an event. The server's and the client's differ.</summary>
internal interface INetworkedEventCoordinatorRegistration
{
    void Register<T>(IDependencyContainer container, INetworkedEventContract<T> contract)
        where T : struct, INetworkedEvent;
}
