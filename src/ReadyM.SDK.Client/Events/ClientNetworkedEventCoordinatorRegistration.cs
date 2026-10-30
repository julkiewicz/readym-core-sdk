using ReadyM.Api.DI;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Client.Events;

/// <summary>Registers the client's coordinator for each declared event, handled here or not.</summary>
internal sealed class ClientNetworkedEventCoordinatorRegistration : INetworkedEventCoordinatorRegistration
{
    public void Register<T>(IDependencyContainer container, INetworkedEventContract<T> contract)
        where T : struct, INetworkedEvent
    {
        container.RegisterSingleton(contract);
        container.RegisterSingleton<ClientNetworkedEventCoordinator<T>>();
    }
}
