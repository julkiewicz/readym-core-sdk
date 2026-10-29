using ReadyM.Api.DI;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Server.Events;

/// <summary>The server's coordinator per declared event, and the contract it reads the event from.</summary>
internal sealed class ServerNetworkedEventRegistration : INetworkedEventRegistration
{
    public void Register<T>(IDependencyContainer container, INetworkedEventContract<T> contract)
        where T : struct, INetworkedEvent
    {
        container.RegisterSingleton(contract);
        container.RegisterSingleton<INetworkedEventDispatch<T>, ServerNetworkedEventCoordinator<T>>();
    }
}
