using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// Makes a shape's component savable under the name [Persisted] gives it, and tells the SDK it is.
internal static class PersistenceEmitter
{
    /// The component's save code. A file of its own, for the same reason a replicated component is: the emitter
    /// writes the usings and the namespace itself.
    public static (string HintName, string Source)? Component(DeclarationModel model, Compilation compilation)
    {
        if (!model.RegistersPersistence)
            return null;

        var described = ReplicatedComponentModel.For(model, compilation);

        if (described is null)
            return null;

        return (
            ArchetypeNames.HintOf(model.Symbol, "Save"),
            SaveComponentEmitter.Emit(described, model.PersistedName!, model.PersistedVersion));
    }

    public static void Emit(SourceWriter writer, DeclarationModel model, Compilation compilation)
    {
        if (!model.RegistersPersistence)
            return;

        writer.Line();
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        using (writer.Braces($"public static class {model.Name}Persistence"))
        {
            if (ExtendsEmitter.HasModuleInitializer(compilation))
                writer.Line("[global::System.Runtime.CompilerServices.ModuleInitializer]");

            writer.Line("public static void Register()");
            writer.Line($"    => {ArchetypeNames.PersistenceRegistry}.Register<{model.QualifiedComponent}>();");
        }
    }
}
