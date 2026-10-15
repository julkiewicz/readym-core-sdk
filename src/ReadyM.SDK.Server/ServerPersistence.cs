using ReadyM.Api.Saves;
using ReadyM.Relay.Server.Sdk.Ecs.Components;
using ReadyM.SDK.Archetypes;

namespace ReadyM.SDK.Server;

/// Tells the relay which of a mod's components are saved with the entities that carry them.
public static class ServerPersistence
{
    /// Returns how many were declared, which a mod can log to see its shapes arrived.
    public static int DeclareAll(IComponentRegistry registry)
    {
        var declare = new DeclareCallback(registry);

        foreach (var persisted in PersistenceRegistry.Snapshot())
            persisted.Accept(declare);

        return declare.Declared;
    }

    private sealed class DeclareCallback(IComponentRegistry registry) : IPersistedComponentCallback
    {
        public int Declared { get; private set; }

        public void Accept<T>() where T : struct, ISavableComponent
        {
            registry.DeclarePersisted<T>();
            Declared++;
        }
    }
}
