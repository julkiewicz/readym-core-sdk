using System.Linq;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Archetypes;

namespace ReadyM.Api.Generators;

/// <summary>
/// Which RPC parameters are shapes, which travel as their entity's network id and are looked up again where the
/// handler runs.
/// </summary>
internal static class RpcEntityParams
{
    public const string Codec = "global::ReadyM.SDK.Entities.RpcEntityCodec";

    /// <summary>Where the server reads, since the codec's reads take the client's handler base.</summary>
    public const string ServerCodec = "global::ReadyM.SDK.Server.Entities.ServerRpcEntityCodec";

    /// <summary>Whether a parameter is an archetype or a mixin, or a nullable one, which is then optional.</summary>
    public static bool IsShape(ITypeSymbol type, out INamedTypeSymbol shape, out bool optional)
    {
        optional = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

        var underlying = optional ? ((INamedTypeSymbol)type).TypeArguments[0] : type;

        shape = (underlying as INamedTypeSymbol)!;
        return shape is not null && (ArchetypeOf(shape) is not null || MixinOf(shape) is not null);
    }

    /// <summary>
    /// Whether an entity of this shape can have a network id: a networked archetype, or a mixin that replicates, which
    /// only ever does on one.
    /// </summary>
    public static bool CanBeOnTheNetwork(INamedTypeSymbol shape)
    {
        if (ArchetypeOf(shape) is { } archetype)
            return !archetype.NamedArguments.Any(a => a.Key == "Networked" && a.Value.Value is false);

        return MixinOf(shape) is { ConstructorArguments.Length: > 0 } mixin
               && mixin.ConstructorArguments[0].Value is { } replication
               && System.Convert.ToInt32(replication) != (int)Replication.None;
    }

    private static AttributeData? ArchetypeOf(INamedTypeSymbol type) => AttributeNamed(type, ArchetypeNames.ArchetypeAttribute);

    private static AttributeData? MixinOf(INamedTypeSymbol type) => AttributeNamed(type, ArchetypeNames.MixinAttribute);

    private static AttributeData? AttributeNamed(INamedTypeSymbol type, string name) =>
        type.GetAttributes().FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == name);
}
