using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// <summary>Declares that a shape's component replicates, and how its changes travel.</summary>
internal static class ReplicationEmitter
{
    public static void Emit(SourceWriter writer, DeclarationModel model)
    {
        if (!model.RegistersReplication)
            return;

        writer.Line();
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        using (writer.Braces($"internal static class {model.Name}Replication"))
        {
            writer.Line($"public static void Register({ArchetypeNames.DependencyContainer} services)");
            writer.Line($"    => {ArchetypeNames.ReplicationRegistrationHelper}.Register<{model.QualifiedComponent}>(");
            writer.Line($"        services, {ArchetypeNames.Delivery}.{model.Delivery});");
        }
    }
}
