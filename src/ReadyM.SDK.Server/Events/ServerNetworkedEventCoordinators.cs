using System.ComponentModel;
using ReadyM.Api.DI;
using ReadyM.SDK.Events;

namespace ReadyM.SDK.Server.Events;

/// <exclude />
/// <summary>The server's coordinator per declared event, and the contract it reads the event from.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ServerNetworkEventCoordinators : INetworkEventCoordinators
{
    public void Register<T>(IDependencyContainer container, INetworkEventContract<T> contract)
        where T : struct, INetworkEvent
    {
        container.RegisterSingleton(contract);
        container.RegisterSingleton<INetworkEventDispatch<T>, ServerNetworkEventCoordinator<T>>();
    }
}
