using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// Checks what a shape asks to persist, before anything is emitted against it.
internal static class PersistenceRules
{
    public static readonly DiagnosticDescriptor ComponentCannotPersist = new(
        "READYM041",
        "Persistence is stated on a borrowed component",
        "'{0}' backs '{1}' and saves through its own ISavableComponent, if at all. Drop [Persisted]: a shape "
        + "cannot save a component it does not own.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NothingToPersist = new(
        "READYM042",
        "Shape has nothing to persist",
        "'{0}' holds no values, so there is nothing to persist.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NameMissing = new(
        "READYM043",
        "Persisted shape has no save name",
        "'{0}' needs a save name in [Persisted]. It is how a save names the component, so it has to be unique "
        + "and must not change once saves exist.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static ImmutableArray<Diagnostic> Check(DeclarationModel model)
    {
        if (model.PersistedName is not { } name)
            return [];

        var found = new List<Diagnostic>();
        var at = model.Symbol.Locations.FirstOrDefault() ?? Location.None;

        if (model.ExplicitComponent is { } component)
            found.Add(Diagnostic.Create(ComponentCannotPersist, at, model.Name, component.ToDisplayString()));
        else if (model.Accessors.Count == 0)
            found.Add(Diagnostic.Create(NothingToPersist, at, model.Name));

        if (string.IsNullOrWhiteSpace(name))
            found.Add(Diagnostic.Create(NameMissing, at, model.Name));

        return [.. found];
    }
}
