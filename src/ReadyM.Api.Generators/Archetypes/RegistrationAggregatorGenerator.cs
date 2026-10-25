using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReadyM.Api.Generators.Archetypes;

/// <summary>
/// Gathers the registrations a compilation emits into one well-known type per concern, which a loader calls with its
/// container: the shapes' components, and their replication. The kinds that still fill the SDK's statics are gathered
/// into ShapeRegistrations.RegisterAll.
/// </summary>
[Generator]
internal class RegistrationAggregatorGenerator : IIncrementalGenerator
{
    private const string Namespace = "ReadyM.SDK.Generated";
    private const string ClassName = "ShapeRegistrations";
    private const string ComponentClassName = "GeneratedComponentRegistrationHelper";
    private const string ReplicationClassName = "GeneratedReplicationRegistrationHelper";

    // Which generated type a registration is listed in
    private const int Components = 0;
    private const int Replication = 1;
    private const int Rest = 2;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var archetypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            ArchetypeNames.ArchetypeAttribute,
            static (node, _) => node is StructDeclarationSyntax,
            Read);

        var mixins = context.SyntaxProvider.ForAttributeWithMetadataName(
            ArchetypeNames.MixinAttribute,
            static (node, _) => node is StructDeclarationSyntax,
            Read);

        var services = context.SyntaxProvider.ForAttributeWithMetadataName(
            ArchetypeNames.ServiceAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            ReadService);

        var entries = context.SyntaxProvider.ForAttributeWithMetadataName(
            ArchetypeNames.ModEntryAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            ReadModEntry);

        var configs = context.SyntaxProvider.ForAttributeWithMetadataName(
            ArchetypeNames.ModConfigAttribute,
            static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
            ReadModConfig);

        var handlers = context.SyntaxProvider.ForAttributeWithMetadataName(
            ArchetypeNames.RpcHandlersForAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            ReadRpcHandlers);

        // The 0.x spelling of the same attribute, so a mod that has not moved over is collected too.
        var legacyHandlers = context.SyntaxProvider.ForAttributeWithMetadataName(
            ArchetypeNames.ServerRpcForAttribute,
            static (node, _) => node is ClassDeclarationSyntax,
            ReadRpcHandlers);

        var all = archetypes.Collect().Combine(mixins.Collect())
            .Combine(services.Collect()).Combine(entries.Collect()).Combine(configs.Collect())
            .Combine(handlers.Collect()).Combine(legacyHandlers.Collect());

        context.RegisterSourceOutput(all, static (spc, found) =>
        {
            var declared = found.Left.Left;

            var all = declared.Left.Left.Left.Left.Concat(declared.Left.Left.Left.Right)
                .Concat(declared.Left.Left.Right).Concat(declared.Left.Right).Concat(declared.Right)
                .Concat(found.Left.Right).Concat(found.Right)
                .SelectMany(entry => entry)
                .Distinct()
                .ToList();

            System.Collections.Generic.List<string> NamesOf(int kind) => all
                .Where(entry => entry.Kind == kind)
                .Select(entry => entry.Name)
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToList();

            if (NamesOf(Components) is { Count: > 0 } components)
                spc.AddSource($"{Namespace}.{ComponentClassName}.g.cs", EmitHelper(ComponentClassName, components));

            if (NamesOf(Replication) is { Count: > 0 } replication)
                spc.AddSource($"{Namespace}.{ReplicationClassName}.g.cs", EmitHelper(ReplicationClassName, replication));

            {
                // FixMe(2026-10-19-twins-scan-sdk): these kinds still fill the SDK's statics through RegisterAll, and
                // archetypes until 2026-10-18-twins-m4. Each gets a helper of its own that registers in the container.
                if (NamesOf(Rest) is { Count: > 0 } rest)
                    spc.AddSource($"{Namespace}.{ClassName}.g.cs", Emit(rest));
            }
        });
    }

    /// <summary>The registration classes one declaration produces, by the names that emit them and what lists them.</summary>
    private static ImmutableArray<(int Kind, string Name)> Read(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return [];

        var model = DeclarationModel.For(symbol);
        var found = ImmutableArray.CreateBuilder<(int Kind, string Name)>();
        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        if (ComponentRegistrationEmitter.Applies(model))
            found.Add((Components, $"{prefix}{ComponentRegistrationEmitter.NameOf(symbol)}"));

        if (model.RegistersReplication)
            found.Add((Replication, $"{prefix}{symbol.Name}Replication"));

        if (model.IsArchetype)
            found.Add((Rest, $"{prefix}{symbol.Name}Declared"));

        if (model.Extends.Count > 0)
            found.Add((Rest, $"{prefix}{symbol.Name}Extends"));

        if (model.Tags.Count > 0)
            found.Add((Rest, $"{prefix}{symbol.Name}Tags"));

        if (NativeInitEmitter.Applies(model))
            found.Add((Rest, $"{prefix}{symbol.Name}NativeInit"));

        // Handlers the shape declared for itself, which sit inside the shape rather than beside it.
        if (CreateHandlerEmitter.Check(model).Length == 0)
            foreach (var handler in model.CreateHandlers)
                found.Add((Rest, $"{prefix}{symbol.Name}.{handler.Name}Registration"));

        if (DeleteHandlerEmitter.Check(model).Length == 0)
            foreach (var handler in model.DeleteHandlers)
                found.Add((Rest, $"{prefix}{symbol.Name}.{handler.Name}DeleteRegistration"));

        return found.ToImmutable();
    }

    /// <summary>The registration an RPC class produces, if a generator will complete it.</summary>
    /// <remarks>
    /// Both sides carry the same attribute and only one of the two generators claims a class, but
    /// the registration it writes has the same name either way, so this does not have to know which.
    /// </remarks>
    private static ImmutableArray<(int Kind, string Name)> ReadRpcHandlers(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol
            || !ServerRpcModel.Implements(symbol))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [(Rest, $"{prefix}{symbol.Name}.Registration")];
    }

    /// <summary>The registration a mod's config produces, unless the analyzer refuses the class.</summary>
    private static ImmutableArray<(int Kind, string Name)> ReadModConfig(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return [];

        if (Mods.ModConfigShape.Read(symbol).Problems.Any(problem => problem.Severity == DiagnosticSeverity.Error))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [(Rest, $"{prefix}{symbol.Name}.Registration")];
    }

    /// <summary>The registration a mod's entry point produces, unless the analyzer refuses the class.</summary>
    private static ImmutableArray<(int Kind, string Name)> ReadModEntry(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return [];

        if (Mods.ModEntryShape.Read(symbol).Problems.Any(problem => problem.Severity == DiagnosticSeverity.Error))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [(Rest, $"{prefix}{symbol.Name}.Registration")];
    }

    /// <summary>The registration a service produces, unless the analyzer refuses the class.</summary>
    private static ImmutableArray<(int Kind, string Name)> ReadService(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return [];

        if (Services.ServiceShape.Read(symbol).Problems.Any(problem => problem.Severity == DiagnosticSeverity.Error))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [(Rest, $"{prefix}{symbol.Name}.Registration")];
    }

    private static string Emit(System.Collections.Generic.IReadOnlyList<string> names)
    {
        var writer = new SourceWriter();

        writer.Line("// <auto-generated/>");
        writer.Line("#nullable enable");
        writer.Line();
        writer.Line($"namespace {Namespace};");
        writer.Line();
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        using (writer.Braces($"internal static class {ClassName}"))
        using (writer.Braces("public static void RegisterAll()"))
            foreach (var name in names)
                writer.Line($"{name}.Register();");

        return writer.ToString();
    }

    /// <summary>One concern's helper: calls each of its registrations with the container a loader passes.</summary>
    private static string EmitHelper(string className, System.Collections.Generic.IReadOnlyList<string> names)
    {
        var writer = new SourceWriter();

        writer.Line("// <auto-generated/>");
        writer.Line("#nullable enable");
        writer.Line();
        writer.Line($"namespace {Namespace};");
        writer.Line();
        writer.Line("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");

        using (writer.Braces($"internal static class {className}"))
        using (writer.Braces($"public static void Register({ArchetypeNames.DependencyContainer} services)"))
            foreach (var name in names)
                writer.Line($"{name}.Register(services);");

        return writer.ToString();
    }
}
