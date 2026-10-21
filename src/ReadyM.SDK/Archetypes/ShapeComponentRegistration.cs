using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;

namespace ReadyM.SDK.Archetypes;

/// <summary>
/// The 1.0 shapes' own components on the "all" twin: what ServerReplication and ServerArchetypes' first loop
/// registered. The replicated ones also reach the networked twin, through <see cref="ReplicatedShapeRegistration"/>.
/// </summary>
internal sealed class ShapeComponentRegistration : IAllComponentRegistration
{
    public void Register(IAllComponentRegistry registry)
    {
        {
            // FixMe(2026-10-19-twins-scan-shapes): reads the SDK's statics, half-processed while loading. It consumes
            // the scanned shape registry instead.
            var registered = new HashSet<Type>();

            foreach (var replicated in ReplicationRegistry.InWireOrder())
                if (registered.Add(replicated.Component))
                    registry.RegisterComponent(replicated.Component);

            foreach (var shape in ArchetypeContributions.Shapes())
                foreach (var component in ArchetypeContributions.OwnGenerated(shape))
                    if (registered.Add(component) && !typeof(INetworkedComponent).IsAssignableFrom(component))
                        registry.RegisterComponent(component);
        }
    }
}
