using ReadyM.Relay.Server.Sdk.Ecs.Components;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Server;

/// <summary>
/// Declares the storage of every component a mod's shapes replicate. How each travels is the networked
/// registry's, see <see cref="ReplicatedShapeRegistration"/>.
/// </summary>
internal static class ServerReplication
{
    /// <summary>Returns how many were registered, which a mod can log to see its shapes arrived.</summary>
    public static int RegisterAll(IComponentRegistry registry)
    {
        var registered = 0;

        foreach (var replicated in ReplicationRegistry.Snapshot())
        {
            registry.RegisterComponent(replicated.Component, Wire(replicated.Delivery));
            registered++;
        }

        return registered;
    }

    // Spelled out rather than cast: the numbering is a contract with the relay, which reads the
    // same byte off the interop struct, and it should not quietly follow a reordering of the enum.
    private static byte Wire(Delivery delivery) => delivery switch
    {
        Delivery.Reliable => 0,
        Delivery.Unreliable => 1,
        _ => 0
    };
}