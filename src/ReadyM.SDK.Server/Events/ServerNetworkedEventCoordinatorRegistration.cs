using ReadyM.Api.DI;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Server.Events;

/// <summary>Registers the server's coordinator for each declared event, handled here or not.</summary>
internal sealed class ServerNetworkedEventCoordinatorRegistration : INetworkedEventCoordinatorRegistration
{
    public void Register<T>(IDependencyContainer container, INetworkedEventContract<T> contract)
        where T : struct, INetworkedEvent
    {
        container.RegisterSingleton(contract);
        container.RegisterSingleton<ServerNetworkedEventCoordinator<T>>();
    }
}
