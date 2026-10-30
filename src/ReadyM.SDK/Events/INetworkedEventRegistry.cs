using System.ComponentModel;
using ReadyM.Api.Multiplayer.RPC;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>Where generated code declares an assembly's networked events and handlers.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkedEventRegistry
{
    /// <summary>Declares an event, from the project that declares it.</summary>
    void Declare<T>(INetworkedEventContract<T> contract) where T : struct, INetworkedEvent;

    /// <summary>Adds one mod's handlers for an event, on one side.</summary>
    void AddHandlers<THandlers, T>(RpcSide side)
        where THandlers : class, INetworkedEventHandlers<T>
        where T : struct, INetworkedEvent;
}
