using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// Registers the tags a shape declares, so every entity created as it carries them.
internal static class TagEmitter
{
    public static void Emit(SourceWriter writer, DeclarationModel model, Compilation compilation)
    {
        if (model.Tags.Count == 0)
            return;

        writer.Line();
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        using (writer.Braces($"public static class {model.Name}Tags"))
        {
            if (ExtendsEmitter.HasModuleInitializer(compilation))
                writer.Line("[global::System.Runtime.CompilerServices.ModuleInitializer]");

            using (writer.Braces("public static void Register()"))
            {
                // typeof rather than a name, so the compiler is the one that says a tag is misspelt
                // or is not visible from here, rather than the host failing to find it at load.
                var tags = string.Join(
                    ", ",
                    model.Tags.Select(tag => $"typeof({tag.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})"));

                writer.Line($"{ArchetypeNames.Registry}.Tag(typeof({model.QualifiedName}), {tags});");
            }
        }
    }
}
