using System.Collections.Generic;
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

    /// <summary>The registration classes one shape produces, by the generated type that lists them.</summary>
    private sealed class ShapeNames
    {
        public readonly List<string> Components = [];
        public readonly List<string> Replication = [];

        // FixMe(2026-10-19-twins-scan-sdk): the kinds ShapeRegistrations.RegisterAll still lists, which fill the SDK's
        // statics: archetypes until 2026-10-18-twins-m4, native init, handlers, mod entries, configs, services and RPC
        // handlers. Each gets a list and a helper of its own.
        public readonly List<string> RegisterAllFixMe = [];
    }

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

        var shapes = archetypes.Collect().Combine(mixins.Collect());
        var others = services.Collect().Combine(entries.Collect()).Combine(configs.Collect())
            .Combine(handlers.Collect()).Combine(legacyHandlers.Collect());

        context.RegisterSourceOutput(shapes.Combine(others), static (spc, found) =>
        {
            var declared = found.Left.Left.Concat(found.Left.Right).ToList();
            var other = found.Right;

            var components = Sorted(declared.SelectMany(names => names.Components));
            var replication = Sorted(declared.SelectMany(names => names.Replication));

            if (components.Count > 0)
                spc.AddSource($"{Namespace}.{ComponentClassName}.g.cs", EmitHelper(ComponentClassName, components));

            if (replication.Count > 0)
                spc.AddSource($"{Namespace}.{ReplicationClassName}.g.cs", EmitHelper(ReplicationClassName, replication));

            {
                // FixMe(2026-10-19-twins-scan-sdk): these kinds still fill the SDK's statics through RegisterAll, and
                // archetypes until 2026-10-18-twins-m4. Each gets a helper of its own that registers in the container.
                var registerAll = Sorted(declared.SelectMany(names => names.RegisterAllFixMe)
                    .Concat(other.Left.Left.Left.Left.Concat(other.Left.Left.Left.Right).Concat(other.Left.Left.Right)
                        .Concat(other.Left.Right).Concat(other.Right).SelectMany(names => names)));

                if (registerAll.Count > 0)
                    spc.AddSource($"{Namespace}.{ClassName}.g.cs", Emit(registerAll));
            }
        });
    }

    // Each name once, in ordinal order, so the generated lists do not depend on declaration order
    private static List<string> Sorted(IEnumerable<string> names)
        => names.Distinct().OrderBy(name => name, System.StringComparer.Ordinal).ToList();

    /// <summary>The registration classes one shape produces, by the names that emit them.</summary>
    private static ShapeNames Read(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        var found = new ShapeNames();

        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return found;

        var model = DeclarationModel.For(symbol);
        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        if (ComponentRegistrationEmitter.Applies(model))
            found.Components.Add($"{prefix}{ComponentRegistrationEmitter.NameOf(symbol)}");

        if (model.RegistersReplication)
            found.Replication.Add($"{prefix}{symbol.Name}Replication");

        if (model.IsArchetype)
            found.RegisterAllFixMe.Add($"{prefix}{symbol.Name}Declared");

        if (model.Extends.Count > 0)
            found.RegisterAllFixMe.Add($"{prefix}{symbol.Name}Extends");

        if (model.Tags.Count > 0)
            found.RegisterAllFixMe.Add($"{prefix}{symbol.Name}Tags");

        if (NativeInitEmitter.Applies(model))
            found.RegisterAllFixMe.Add($"{prefix}{symbol.Name}NativeInit");

        // Handlers the shape declared for itself, which sit inside the shape rather than beside it.
        if (CreateHandlerEmitter.Check(model).Length == 0)
            foreach (var handler in model.CreateHandlers)
                found.RegisterAllFixMe.Add($"{prefix}{symbol.Name}.{handler.Name}Registration");

        if (DeleteHandlerEmitter.Check(model).Length == 0)
            foreach (var handler in model.DeleteHandlers)
                found.RegisterAllFixMe.Add($"{prefix}{symbol.Name}.{handler.Name}DeleteRegistration");

        return found;
    }

    /// <summary>The registration an RPC class produces, if a generator will complete it.</summary>
    /// <remarks>
    /// Both sides carry the same attribute and only one of the two generators claims a class, but
    /// the registration it writes has the same name either way, so this does not have to know which.
    /// </remarks>
    private static ImmutableArray<string> ReadRpcHandlers(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol
            || !ServerRpcModel.Implements(symbol))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [$"{prefix}{symbol.Name}.Registration"];
    }

    /// <summary>The registration a mod's config produces, unless the analyzer refuses the class.</summary>
    private static ImmutableArray<string> ReadModConfig(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return [];

        if (Mods.ModConfigShape.Read(symbol).Problems.Any(problem => problem.Severity == DiagnosticSeverity.Error))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [$"{prefix}{symbol.Name}.Registration"];
    }

    /// <summary>The registration a mod's entry point produces, unless the analyzer refuses the class.</summary>
    private static ImmutableArray<string> ReadModEntry(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return [];

        if (Mods.ModEntryShape.Read(symbol).Problems.Any(problem => problem.Severity == DiagnosticSeverity.Error))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [$"{prefix}{symbol.Name}.Registration"];
    }

    /// <summary>The registration a service produces, unless the analyzer refuses the class.</summary>
    private static ImmutableArray<string> ReadService(GeneratorAttributeSyntaxContext context, CancellationToken ct)
    {
        if (context.TargetSymbol is not INamedTypeSymbol { ContainingType: null } symbol)
            return [];

        if (Services.ServiceShape.Read(symbol).Problems.Any(problem => problem.Severity == DiagnosticSeverity.Error))
            return [];

        var ns = ArchetypeNames.NamespaceOf(symbol);
        var prefix = ns.Length == 0 ? "global::" : $"global::{ns}.";

        return [$"{prefix}{symbol.Name}.Registration"];
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
