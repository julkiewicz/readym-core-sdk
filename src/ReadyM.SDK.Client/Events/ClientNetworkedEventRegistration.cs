using ReadyM.Api.DI;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Client.Events;

/// <summary>
/// The client's coordinator per declared event, and the contract it reads the event from. Every event the
/// process knows about gets one, handled by a mod here or not: a client that only raises an event still needs
/// somewhere for a refusal of it to land.
/// </summary>
internal sealed class ClientNetworkedEventRegistration : INetworkedEventRegistration
{
    public void Register<T>(IDependencyContainer container, INetworkedEventContract<T> contract)
        where T : struct, INetworkedEvent
    {
        container.RegisterSingleton(contract);
        container.RegisterSingleton<ClientNetworkedEventCoordinator<T>>();
    }
}
