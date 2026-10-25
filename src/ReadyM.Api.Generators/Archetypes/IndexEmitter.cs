using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// <summary>Declares a shape as indexed, and names the component that holds the index behind it.</summary>
internal static class IndexEmitter
{
    /// <summary>The extra contract an indexed shape carries, so a lookup can name it.</summary>
    public static string Contract(DeclarationModel model)
        => model.IndexedBy is null ? string.Empty : $", {ArchetypeNames.Indexed}<{model.IndexedBy}>";

    /// <summary>Inside the shape: hands a finder the component behind its index, so a lookup needs no registry.</summary>
    public static void Emit(SourceWriter writer, DeclarationModel model)
    {
        if (model.IndexedBy is null)
            return;

        writer.Line();
        writer.Line(
            $"bool {ArchetypeNames.Indexed}<{model.IndexedBy}>.TryFind<TFinder>(ref TFinder finder, {model.IndexedBy} key, "
            + $"out {ArchetypeNames.RawEntity} entity)");
        writer.Line($"    => finder.TryFind<{model.QualifiedComponent}>(key, out entity);");
    }
}
