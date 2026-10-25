using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// <summary>
/// Registers the components a shape brings on the "all" twin: the one the generator emits for it, an archetype's
/// marker, and a borrowed one declared in the same assembly that is not networked. Each is brought by exactly one
/// shape, so each is registered once.
/// </summary>
internal static class ComponentRegistrationEmitter
{
    public static string NameOf(INamedTypeSymbol symbol) => symbol.Name + "ComponentRegistrationHelper";

    public static bool Applies(DeclarationModel model) => Components(model).Any();

    public static void Emit(SourceWriter writer, DeclarationModel model)
    {
        if (!Applies(model))
            return;

        writer.Line();
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        using (writer.Braces($"internal static class {NameOf(model.Symbol)}"))
        using (writer.Braces($"public static void Register({ArchetypeNames.DependencyContainer} services)"))
        {
            foreach (var component in Components(model))
                writer.Line($"{ArchetypeNames.ComponentRegistrationHelper}.Register<{component}>(services);");
        }
    }

    private static System.Collections.Generic.IEnumerable<string> Components(DeclarationModel model)
    {
        if (model.EmitsComponent)
            yield return model.QualifiedComponent;
        else if (model.ExplicitComponent is { } borrowed
                 && SymbolEqualityComparer.Default.Equals(borrowed.ContainingAssembly, model.Symbol.ContainingAssembly)
                 && !borrowed.AllInterfaces.Any(contract => "global::" + contract.ToDisplayString() == ArchetypeNames.NetworkedComponent))
            yield return model.QualifiedComponent;

        if (model.NeedsMarker)
            yield return model.QualifiedMarker;
    }
}
