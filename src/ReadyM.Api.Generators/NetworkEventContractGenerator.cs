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
    private const string Attributes = "global::ReadyM.SDK.Attributes";
    private const string Contract = "global::ReadyM.SDK.Events.INetworkEventContract";
    private const string Core = "global::ReadyM.SDK.Events.NetworkEventDispatchCore";
    private const string Codec = "global::ReadyM.SDK.Events.NetworkEventCodec";
    private const string Handle = "global::ReadyM.SDK.Entities.EntityHandle";

    private const string EditorBrowsable =
        "global::System.ComponentModel.EditorBrowsable("
        + "global::System.ComponentModel.EditorBrowsableState.Never)";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var events = context.SyntaxProvider
            .CreateSyntaxProvider(Predicate, Transform)
            .Where(e => e is not null)
            .Collect();

        context.RegisterSourceOutput(context.CompilationProvider.Combine(events), GenerateSources);
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

    private static void GenerateSources(
        SourceProductionContext context, (Compilation Compilation, ImmutableArray<EventInfo?> Events) input)
    {
        var events = input.Events.Where(e => e is not null).Select(e => e!)
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

        EmitDeclarations(context, input.Compilation, events);
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
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using LiteNetLib.Utils;");
        sb.AppendLine();
        sb.AppendLine($"namespace {declared.Symbol.ContainingNamespace.ToDisplayString()};");
        sb.AppendLine();
        sb.AppendLine($"public partial struct {declared.Symbol.Name} : {Marker}");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>This event's code on the wire, once the host has placed its mod's block.</summary>");
        sb.AppendLine("    public global::ReadyM.Api.Multiplayer.Protocol.Enums.RelayMessageCode Code");
        sb.AppendLine($"        => {ManifestClassName}.{declared.Symbol.Name}Code + {ManifestClassName}.Offset;");
        sb.AppendLine();
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
        sb.AppendLine();

        EmitContract(sb, declared);

        context.AddSource($"{declared.Symbol.Name}.NetworkEvent.g.cs", sb.ToString());
    }

    /// <summary>
    /// Everything about the event that its handlers do not decide: where it goes, who may raise it, and how it
    /// reads back off the wire. One per event rather than one per mod, which is what lets the coordinator that
    /// runs every mod's handlers check the policy once and fan the event out once.
    /// </summary>
    private static void EmitContract(StringBuilder sb, EventInfo declared)
    {
        var name = declared.Symbol.Name;
        var type = declared.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var subject = declared.Subjects.FirstOrDefault();

        sb.AppendLine("/// <exclude/>");
        sb.AppendLine($"/// <summary>What {name} is, for the coordinator that runs its handlers.</summary>");
        sb.AppendLine($"[{EditorBrowsable}]");
        sb.AppendLine($"public sealed class {name}Contract : {Contract}<{type}>");
        sb.AppendLine("{");
        sb.AppendLine("    public global::ReadyM.Api.Multiplayer.Protocol.Enums.RelayMessageCode Code");
        sb.AppendLine($"        => {ManifestClassName}.{name}Code + {ManifestClassName}.Offset;");
        sb.AppendLine();
        sb.AppendLine($"    public {Attributes}.Scope Scope => {Attributes}.Scope.{declared.Scope};");
        sb.AppendLine();
        sb.AppendLine($"    public {Attributes}.Policy Policy => {Attributes}.Policy.{declared.Policy};");
        sb.AppendLine();
        sb.AppendLine($"    public {Handle}? Subject(in {type} e)");
        sb.AppendLine($"        => {Subject(subject)};");
        sb.AppendLine();
        sb.AppendLine($"    public global::ReadyM.Api.Idents.PlayerId Target(in {type} e)");
        sb.AppendLine($"        => {(declared.Target is { } target ? $"e.{target.Name}.PlayerId" : "global::ReadyM.Api.Idents.PlayerId.Invalid")};");
        sb.AppendLine();

        EmitRead(sb, declared, type);
        EmitReason(sb, declared);

        sb.AppendLine("}");
    }

    /// <summary>The entity the event is about, as a handle the server checks ownership and scope against.</summary>
    private static string Subject(IPropertySymbol? subject)
    {
        if (subject is null)
            return "null";

        return RpcEntityParams.IsShape(subject.Type, out _, out var optional) && optional
            ? $"e.{subject.Name} is {{ }} subject ? {Handle}.Of(subject) : null"
            : $"{Handle}.Of(e.{subject.Name})";
    }

    /// <summary>
    /// Reads the event back in the order it wrote itself. An entity member names an id this end may not have,
    /// so a miss drops the whole event: half an event is worse than none, and the sender's own end has already
    /// been told whether it was accepted.
    /// </summary>
    private static void EmitRead(StringBuilder sb, EventInfo declared, string type)
    {
        sb.AppendLine($"    public bool TryRead({Core} world, NetDataReader reader, out {type} e)");
        sb.AppendLine("    {");
        sb.AppendLine("        e = default;");
        sb.AppendLine();

        for (var i = 0; i < declared.Members.Count; i++)
        {
            var member = declared.Members[i];

            if (RpcEntityParams.IsShape(member.Type, out var shape, out var optional))
            {
                var read = optional ? "TryReadOptional" : "TryRead";
                var named = shape.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                sb.AppendLine(
                    $"        if (!{Codec}.{read}<{named}>("
                    + $"world, reader, out var m{i}, \"{declared.Symbol.Name}\", \"{member.Name}\"))");
                sb.AppendLine("            return false;");
            }
            else
            {
                sb.AppendLine($"        var m{i} = {Reader(member.Type)};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"        e = new {type}");
        sb.AppendLine("        {");

        for (var i = 0; i < declared.Members.Count; i++)
            sb.AppendLine($"            {declared.Members[i].Name} = m{i},");

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>
    /// A refusal's reason, of the type the declaration named. It gets a presence byte because an event may
    /// name a reject type and still be refused without one, by the policy or by a bare Cancel().
    /// </summary>
    private static void EmitReason(StringBuilder sb, EventInfo declared)
    {
        sb.AppendLine("    public void WriteReason(NetDataWriter writer, object? reason)");

        if (declared.Reject is { } reject)
        {
            var named = reject.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var value = reject.TypeKind == TypeKind.Enum ? "(int)given" : "given";

            sb.AppendLine("    {");
            sb.AppendLine($"        if (reason is {named} given)");
            sb.AppendLine("        {");
            sb.AppendLine("            writer.Put((byte)1);");
            sb.AppendLine($"            writer.Put({value});");
            sb.AppendLine("        }");
            sb.AppendLine("        else");
            sb.AppendLine("        {");
            sb.AppendLine("            writer.Put((byte)0);");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine();

            var read = reject.TypeKind == TypeKind.Enum
                ? $"({named})reader.GetInt()"
                : $"reader.Get<{named}>()";

            sb.AppendLine("    public object? ReadReason(NetDataReader reader)");
            sb.AppendLine($"        => reader.GetByte() == 1 ? {read} : (object?)null;");
        }
        else
        {
            // Nothing to write and nothing to read: an event that named no reject type is refused bare
            sb.AppendLine("    {");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    public object? ReadReason(NetDataReader reader) => null;");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Declares every event this assembly holds, where both sides can find it. It runs itself, because a mod
    /// names its events and nothing else: a module initializer runs it where the target has one, and the mod
    /// loader runs it for every assembly it loads, so it has to be safe to say twice.
    /// </summary>
    private static void EmitDeclarations(
        SourceProductionContext context, Compilation compilation, List<EventInfo> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine("namespace ReadyM.SDK.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <exclude/>");
        sb.AppendLine("/// <summary>Where this assembly's network events are declared to the SDK.</summary>");
        sb.AppendLine($"[{EditorBrowsable}]");
        sb.AppendLine("public static class NetworkEventDeclarations");
        sb.AppendLine("{");
        sb.AppendLine("    private static bool _declared;");
        sb.AppendLine();

        if (Archetypes.ExtendsEmitter.HasModuleInitializer(compilation))
            sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");

        sb.AppendLine("    public static void DeclareAll()");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_declared)");
        sb.AppendLine("            return;");
        sb.AppendLine();
        sb.AppendLine("        _declared = true;");
        sb.AppendLine();

        foreach (var declared in events)
        {
            var type = declared.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var contract = $"global::{declared.Symbol.ContainingNamespace.ToDisplayString()}.{declared.Symbol.Name}Contract";

            sb.AppendLine($"        global::ReadyM.SDK.Events.NetworkEventRegistry.Declare<{type}>(new {contract}());");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("NetworkEventDeclarations.g.cs", sb.ToString());
    }

    /// <summary>How one plain member reads itself back, cast where the wire carries the underlying type.</summary>
    private static string Reader(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            var underlying = SerializationHelper.GetEnumBaseType(type);
            var named = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            return $"({named})reader.{SerializationHelper.GetDeserializationMethod(underlying)}()";
        }

        if (SerializationHelper.IsINetSerializable(type))
            return $"reader.Get<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>()";

        return $"reader.{SerializationHelper.GetDeserializationMethod(type.SpecialType)}()";
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

            Reject = declaration.NamedArguments
                .FirstOrDefault(n => n.Key == "Reject").Value.Value as INamedTypeSymbol;

            Members = [.. symbol.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic && p.SetMethod is not null)];
            Subjects = [.. Members.Where(m => Marked(m, "SubjectAttribute"))];
            Target = Members.FirstOrDefault(m => Marked(m, "TargetAttribute"));
            ScopeMember = Members.FirstOrDefault(m => Marked(m, "AreaAttribute") || Marked(m, "CellAttribute"));
        }

        public INamedTypeSymbol Symbol { get; }

        public Scope Scope { get; }

        public Policy Policy { get; }

        /// <summary>The type a refusal carries back, where the declaration named one.</summary>
        public INamedTypeSymbol? Reject { get; }

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
