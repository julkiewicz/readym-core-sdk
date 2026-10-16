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

    public static ImmutableArray<Diagnostic> Check(DeclarationModel model)
    {
        var found = new List<Diagnostic>();

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
