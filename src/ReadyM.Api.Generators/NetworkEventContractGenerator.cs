using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReadyM.Api.Generators;

/// <summary>
/// Runs on the Common project. For each struct marked <c>[NetworkEvent]</c>, emits the marker interface, the
/// serialization of its members, and the manifest entry that gives it the same id on both ends. An event is only
/// ever sent to the server, which decides who else receives it, so every event takes one code out of the
/// server-directed space.
/// </summary>
[Generator]
internal class NetworkEventContractGenerator : IIncrementalGenerator
{
    private const string ManifestClassName = "NetworkEventManifest";
    private const string Marker = "global::ReadyM.SDK.Events.INetworkEvent";
    private const string Attribute = "ReadyM.SDK.Attributes.NetworkEventAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var events = context.SyntaxProvider
            .CreateSyntaxProvider(Predicate, Transform)
            .Where(e => e is not null)
            .Collect();

        context.RegisterSourceOutput(events, GenerateSources);
    }

    private static bool Predicate(SyntaxNode node, CancellationToken _)
        => node is StructDeclarationSyntax { AttributeLists.Count: > 0 };

    private static EventInfo? Transform(GeneratorSyntaxContext context, CancellationToken _)
    {
        if (context.Node is not StructDeclarationSyntax syntax)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol)
            return null;

        var declaration = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == Attribute);

        return declaration is null ? null : new EventInfo(symbol, declaration);
    }

    private static void GenerateSources(SourceProductionContext context, ImmutableArray<EventInfo?> raw)
    {
        var events = raw.Where(e => e is not null).Select(e => e!)
            .OrderBy(e => e.Symbol.Name, System.StringComparer.Ordinal)
            .ToList();

        if (events.Count == 0)
            return;

        foreach (var declared in events)
        {
            Validate(context, declared);
            EmitEvent(context, declared);
        }

        EmitManifest(
            context,
            events[0].Symbol.ContainingNamespace.ToDisplayString(),
            events[0].Symbol.ContainingAssembly.Name,
            events);
    }

    /// <summary>What a declaration may not say. Each of these is a hard error rather than a silent oddity.</summary>
    private static void Validate(SourceProductionContext context, EventInfo declared)
    {
        var location = declared.Symbol.Locations.FirstOrDefault();

        if (declared.Subjects.Count > 1)
            Report(context, location, "NEVT005", "Two subjects",
                $"'{declared.Symbol.Name}' marks {declared.Subjects.Count} members [Subject]. An event is about one entity.");

        if (declared.Scope == Scope.Single && declared.Target is null)
            Report(context, location, "NEVT001", "Missing [Target]",
                $"'{declared.Symbol.Name}' is Scope.Single, so one member must be marked [Target] to say which player it goes to.");

        if (declared.Scope != Scope.Single && declared.Target is not null)
            Report(context, location, "NEVT002", "Unused [Target]",
                $"'{declared.Symbol.Name}' is Scope.{declared.Scope}, which goes to an audience rather than one player, so [Target] means nothing on it.");

        if (declared.Scope is Scope.Area or Scope.Cell && declared.Subjects.Count == 0 && declared.ScopeMember is null)
            Report(context, location, "NEVT003", "No audience",
                $"'{declared.Symbol.Name}' is Scope.{declared.Scope} with no [Subject] to take its audience from. Mark a subject, or an [{declared.Scope}] member naming it.");

        if (NeedsSubject(declared.Policy) && declared.Subjects.Count == 0)
            Report(context, location, "NEVT004", "No subject to check",
                $"'{declared.Symbol.Name}' says Policy.{declared.Policy}, which checks the subject, so one member must be marked [Subject].");

        foreach (var member in declared.Members)
        {
            if (RpcEntityParams.IsShape(member.Type, out var shape, out _) && !RpcEntityParams.CanBeOnTheNetwork(shape))
                Report(context, member.Locations.FirstOrDefault() ?? location, "NEVT006", "Shape cannot travel",
                    $"'{declared.Symbol.Name}.{member.Name}' is '{shape.Name}', whose entities never have a network id, so it cannot be sent.");
        }
    }

    private static bool NeedsSubject(Policy policy)
        => policy is Policy.SubjectOwner or Policy.CellMaster or Policy.AreaMaster;

    private static void EmitEvent(SourceProductionContext context, EventInfo declared)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using LiteNetLib.Utils;");
        sb.AppendLine();
        sb.AppendLine($"namespace {declared.Symbol.ContainingNamespace.ToDisplayString()};");
        sb.AppendLine();
        sb.AppendLine($"public partial struct {declared.Symbol.Name} : {Marker}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Writes this event's members, in the order they are declared.</summary>");
        sb.AppendLine("    public void Serialize(NetDataWriter writer)");
        sb.AppendLine("    {");

        foreach (var member in declared.Members)
        {
            if (RpcEntityParams.IsShape(member.Type, out _, out var optional))
            {
                var write = optional ? "WriteOptional" : "Write";
                sb.AppendLine($"        {RpcEntityParams.Codec}.{write}(writer, {member.Name}, \"{member.Name}\");");
            }
            else
            {
                sb.AppendLine($"        writer.Put({member.Name});");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource($"{declared.Symbol.Name}.NetworkEvent.g.cs", sb.ToString());
    }

    /// <summary>
    /// One manifest per assembly, named for it, with a code per event assigned in name order so that every
    /// process agrees on them whatever order it read the declarations in.
    /// </summary>
    private static void EmitManifest(
        SourceProductionContext context, string ns, string manifestId, List<EventInfo> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using ReadyM.Api.Multiplayer.Protocol.Enums;");
        sb.AppendLine();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("/// <exclude/>");
        sb.AppendLine("/// <summary>Where this mod's network events get their wire codes.</summary>");
        sb.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        sb.AppendLine($"public static class {ManifestClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>This set's stable identity: the assembly that declares it.</summary>");
        sb.AppendLine($"    public const string Id = \"{manifestId}\";");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>How many codes this set takes out of the server-directed space.</summary>");
        sb.AppendLine($"    public const byte TotalEventCount = {events.Count};");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>Set once at mod startup, before any event is raised or received.</summary>");
        sb.AppendLine("    public static byte Offset { get; set; }");
        sb.AppendLine();

        for (var i = 0; i < events.Count; i++)
        {
            sb.AppendLine($"    public const RelayMessageCode {events[i].Symbol.Name}Code =");
            sb.AppendLine($"        (RelayMessageCode)(RelayMessageCode.MinServerRpcEvent + {i});");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        context.AddSource($"{ManifestClassName}.g.cs", sb.ToString());
    }

    private static void Report(
        SourceProductionContext context, Location? location, string id, string title, string message)
        => context.ReportDiagnostic(Diagnostic.Create(
            new DiagnosticDescriptor(id, title, message, "NetworkEvent", DiagnosticSeverity.Error, true),
            location));

    /// <summary>One declared event, with what its attribute and its members said.</summary>
    private sealed class EventInfo
    {
        public EventInfo(INamedTypeSymbol symbol, AttributeData declaration)
        {
            Symbol = symbol;

            Scope = declaration.ConstructorArguments.Length > 0
                ? AttributeUtils.EnumArgument(declaration.ConstructorArguments[0], Scope.Server)
                : Scope.Server;

            Policy = declaration.ConstructorArguments.Length > 1
                ? AttributeUtils.EnumArgument(declaration.ConstructorArguments[1], Policy.ServerAuthoritative)
                : Policy.ServerAuthoritative;

            Members = [.. symbol.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic)];
            Subjects = [.. Members.Where(m => Marked(m, "SubjectAttribute"))];
            Target = Members.FirstOrDefault(m => Marked(m, "TargetAttribute"));
            ScopeMember = Members.FirstOrDefault(m => Marked(m, "AreaAttribute") || Marked(m, "CellAttribute"));
        }

        public INamedTypeSymbol Symbol { get; }

        public Scope Scope { get; }

        public Policy Policy { get; }

        public List<IPropertySymbol> Members { get; }

        public List<IPropertySymbol> Subjects { get; }

        public IPropertySymbol? Target { get; }

        public IPropertySymbol? ScopeMember { get; }

        private static bool Marked(ISymbol member, string attribute)
            => member.GetAttributes().Any(a => a.AttributeClass?.Name == attribute);
    }

    /// <summary>Mirrors <c>ReadyM.SDK.Attributes.Scope</c>, which a generator cannot reference.</summary>
    private enum Scope
    {
        Server,
        Global,
        Single,
        Area,
        Cell,
    }

    /// <summary>Mirrors <c>ReadyM.SDK.Attributes.Policy</c>. Read by name, so the order here is cosmetic.</summary>
    private enum Policy
    {
        Anyone,
        SubjectOwner,
        CellMaster,
        AreaMaster,
        ServerAuthoritative,
    }
}
