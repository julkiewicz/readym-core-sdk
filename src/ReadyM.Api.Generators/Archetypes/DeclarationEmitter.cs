using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// <summary>
/// Tells the SDK the archetype exists, which is what a host reads to hand its components over.
/// </summary>
/// <remarks>
/// Every archetype, not only one something extends or creates. A shape a mod keeps to itself has
/// components the host has never heard of until this, and the first query for it asks the host for
/// an id of a component that was never registered.
/// </remarks>
internal static class DeclarationEmitter
{
    public static void Emit(SourceWriter writer, DeclarationModel model, Compilation compilation)
    {
        writer.Line();
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        using (writer.Braces($"public static class {model.Name}Declared"))
        {
            if (ExtendsEmitter.HasModuleInitializer(compilation))
                writer.Line("[global::System.Runtime.CompilerServices.ModuleInitializer]");

            using (writer.Braces("public static void Register()"))
                writer.Line($"{ArchetypeNames.Registry}.Declare(typeof({model.QualifiedName}));");
        }
    }
}
