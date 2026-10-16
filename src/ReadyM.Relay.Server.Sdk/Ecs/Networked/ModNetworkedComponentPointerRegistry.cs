using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Api.Multiplayer.Interop;

namespace ReadyM.Relay.Server.Sdk.Ecs.Networked;

/// <summary>
/// The callbacks the relay replicates each mod networked component through, by full type name. The mod-side
/// counterpart of the relay's <c>NetworkedComponentJobRegistry</c>, built the same way: every registration
/// visits the finished <see cref="INetworkedComponentRegistry"/> and adds what it contributes.
/// </summary>
internal sealed class ModNetworkedComponentPointerRegistry
{
    private readonly Dictionary<string, ModNetworkedComponentInfo> _byName = new(StringComparer.Ordinal);

    public ModNetworkedComponentPointerRegistry(
        INetworkedComponentRegistry registry,
        IEnumerable<IModNetworkedComponentPointerRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            registration.Register(registry, this);
        }
    }

    public IReadOnlyDictionary<string, ModNetworkedComponentInfo> Components => _byName;

    public void Register(string typeFullName, ModNetworkedComponentInfo info)
    {
        if (!_byName.TryAdd(typeFullName, info))
            throw new InvalidOperationException($"Networked mod component '{typeFullName}' already has its callbacks.");
    }
}
