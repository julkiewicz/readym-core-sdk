using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace ReadyM.Api.Generators.Archetypes;

/// <summary>
/// Attributes that mean something on one kind of shape only, or once per shape. [Index] names the one value a shape is
/// found by, [Tag] goes on the archetype entities are created as, and [Extends] adds a mixin to an archetype.
/// </summary>
internal static class ShapeAttributeRules
{
    public static readonly DiagnosticDescriptor SecondIndex = new(
        "READYM047",
        "More than one [Index] on a shape",
        "'{0}' marks both '{1}' and '{2}' with [Index], but a shape is found by one value. Keep [Index] on one of them.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TagOnMixin = new(
        "READYM048",
        "[Tag] on a mixin",
        "'{0}' is a mixin, so [Tag(typeof({1}))] would never be put on an entity: tags go on the archetype entities are "
        + "created as. Move it to an archetype.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExtendsOnArchetype = new(
        "READYM049",
        "[Extends] on an archetype",
        "'{0}' is an archetype, but [Extends(typeof({1}))] adds a mixin to an archetype. Move the values to a mixin and "
        + "put [Extends] on that.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PredictableUnreplicated = new(
        "READYM050",
        "[Predictable] on a value nobody replicates",
        "'{0}.{1}' is not replicated, so there is no server value for a guess at it to fall through to when it is "
        + "taken back. Replicate the shape, or roll the guess back yourself with .Rollback().",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PredictableIndex = new(
        "READYM051",
        "[Predictable] on the value a shape is found by",
        "'{0}.{1}' carries [Index], so entities are found by it. A guess is held beside the entity and moves no "
        + "index, so a lookup would disagree with a read. Keep [Predictable] off the indexed value.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PredictableUnwritable = new(
        "READYM052",
        "[Predictable] on a value that cannot be written",
        "'{0}.{1}' has no setter, so nothing can guess at it. Give it one, or drop [Predictable].",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor PredictableCollection = new(
        "READYM053",
        "[Predictable] on a collection",
        "'{0}.{1}' is a collection, which changes through its own members rather than by assignment, so there is no "
        + "one write to hold as a guess. Guess at a value instead, or roll the change back with .Rollback().",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static ImmutableArray<Diagnostic> Check(DeclarationModel model)
    {
        var found = new List<Diagnostic>();

        foreach (var guessable in model.Accessors.Where(accessor => accessor.IsPredictable))
            Predictable(found, model, guessable);

        var indexes = model.Accessors.Where(accessor => accessor.IsIndex).ToList();

        foreach (var second in indexes.Skip(1))
        {
            var location = LocationOf((ISymbol?)second.Declared ?? model.Symbol, ArchetypeNames.IndexAttribute);
            found.Add(Diagnostic.Create(SecondIndex, location, model.Name, indexes[0].Name, second.Name));
        }

        foreach (var attribute in model.Symbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.ToDisplayString();
            var named = attribute.ConstructorArguments.Length > 0
                ? (attribute.ConstructorArguments[0].Value as INamedTypeSymbol)?.Name
                : null;

            if (named is null)
                continue;

            if (name == ArchetypeNames.TagAttribute && !model.IsArchetype)
                found.Add(Diagnostic.Create(TagOnMixin, LocationOf(attribute, model.Symbol), model.Name, named));
            else if (name == ArchetypeNames.ExtendsAttribute && model.IsArchetype)
                found.Add(Diagnostic.Create(ExtendsOnArchetype, LocationOf(attribute, model.Symbol), model.Name, named));
        }

        return [.. found];
    }

    /// <summary>
    /// Where a guess cannot work. Each is an error rather than a guess that quietly never clears, never shows,
    /// or leaves a lookup and a read disagreeing about the same entity.
    /// </summary>
    private static void Predictable(List<Diagnostic> found, DeclarationModel model, AccessorModel accessor)
    {
        var location = accessor.Declared is { } declared
            ? LocationOf(declared, ArchetypeNames.PredictableAttribute)
            : model.Symbol.Locations.FirstOrDefault() ?? Location.None;

        if (!model.IsReplicated)
            found.Add(Diagnostic.Create(PredictableUnreplicated, location, model.Name, accessor.Name));

        if (accessor.IsIndex)
            found.Add(Diagnostic.Create(PredictableIndex, location, model.Name, accessor.Name));

        if (!accessor.HasSetter)
            found.Add(Diagnostic.Create(PredictableUnwritable, location, model.Name, accessor.Name));

        if (accessor.IsNativeContainer)
            found.Add(Diagnostic.Create(PredictableCollection, location, model.Name, accessor.Name));
    }

    private static Location LocationOf(ISymbol symbol, string attributeName)
    {
        var attribute = symbol.GetAttributes()
            .FirstOrDefault(candidate => candidate.AttributeClass?.ToDisplayString() == attributeName);

        return attribute is null ? symbol.Locations.FirstOrDefault() ?? Location.None : LocationOf(attribute, symbol);
    }

    private static Location LocationOf(AttributeData attribute, ISymbol owner)
        => attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
           ?? owner.Locations.FirstOrDefault()
           ?? Location.None;
}
