using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ReadyM.Api.Generators;

/// <summary>
/// Runs on the Common project. For each struct marked <c>[NetworkedEvent]</c>, emits the marker interface, the
/// serialization of its members, and the manifest entry that gives it the same id on both ends. An event is only
/// ever sent to the server, which decides who else receives it, so every event takes one code out of the
/// server-directed space.
/// </summary>
[Generator]
internal class NetworkedEventContractGenerator : IIncrementalGenerator
{
    private const string ManifestClassName = "NetworkedEventManifest";
    private const string NetworkedEventInterface = "global::ReadyM.SDK.Events.INetworkedEvent";
    private const string NetworkedEventAttribute = "ReadyM.SDK.Attributes.NetworkedEventAttribute";
    private const string AttributesNamespace = "global::ReadyM.SDK.Attributes";
    private const string ContractInterface = "global::ReadyM.SDK.Events.INetworkedEventContract";
    private const string EntityReader = "global::ReadyM.SDK.Events.INetworkedEventEntityReader";
    private const string Registry = "global::ReadyM.SDK.Events.INetworkedEventRegistry";
    private const string EntityHandle = "global::ReadyM.SDK.Entities.EntityHandle";
    private const string AreaId = "global::ReadyM.Api.Idents.AreaId";
    private const string FullCellId = "global::ReadyM.Api.Idents.FullCellId";
    private const string AreaShape = "global::ReadyM.SDK.Archetypes.Core.Area";
    private const string CellShape = "global::ReadyM.SDK.Archetypes.Core.Cell";

    /// <summary>Where a component's vector fields are written from and read into, which an event's share.</summary>
    private const string VectorSerialization = "global::ReadyM.Api.Multiplayer.Extensions.VectorSerializationExtensions";

    private const string EditorBrowsable =
        "global::System.ComponentModel.EditorBrowsable("
        + "global::System.ComponentModel.EditorBrowsableState.Never)";

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

    private static DeclaredEvent? Transform(GeneratorSyntaxContext context, CancellationToken _)
    {
        if (context.Node is not StructDeclarationSyntax syntax)
            return null;

        if (context.SemanticModel.GetDeclaredSymbol(syntax) is not INamedTypeSymbol symbol)
            return null;

        var declaration = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NetworkedEventAttribute);

        return declaration is null ? null : new DeclaredEvent(symbol, declaration);
    }

    private static void GenerateSources(
        SourceProductionContext context, ImmutableArray<DeclaredEvent?> declarations)
    {
        var events = declarations.Where(e => e is not null).Select(e => e!)
            .OrderBy(e => e.Symbol.Name, System.StringComparer.Ordinal)
            .ToList();

        if (events.Count == 0)
            return;

        // An event that cannot be sent is left out of everything generated rather than taking the rest with it.
        // Its error fails the build either way; this keeps that error the only one the mod author sees.
        var sendable = events.Where(declared => Validate(context, declared)).ToList();

        if (sendable.Count == 0)
            return;

        foreach (var declared in sendable)
            EmitEvent(context, declared);

        EmitManifest(
            context,
            sendable[0].Symbol.ContainingNamespace.ToDisplayString(),
            sendable[0].Symbol.ContainingAssembly.Name,
            sendable);

        EmitDeclarations(context, sendable);
    }

    /// <summary>What a declaration may not say. Each of these is a hard error rather than a silent oddity.</summary>
    /// <returns>Whether the event can be generated at all, which a member with no way onto the wire rules out.</returns>
    private static bool Validate(SourceProductionContext context, DeclaredEvent declared)
    {
        var sendable = true;

        var location = declared.Symbol.Locations.FirstOrDefault();

        if (declared.Subjects.Count > 1)
            Report(context, location, "NEVT005", "Two subjects",
                $"'{declared.Symbol.Name}' marks {declared.Subjects.Count} members [Subject]. An event is about one entity.");

        if (declared.Scope == EventScope.Single && declared.Target is null)
            Report(context, location, "NEVT001", "Missing [Target]",
                $"'{declared.Symbol.Name}' is EventScope.Single, so one member must be marked [Target] to say which player it goes to.");

        if (declared.Scope != EventScope.Single && declared.Target is not null)
            Report(context, location, "NEVT002", "Unused [Target]",
                $"'{declared.Symbol.Name}' is EventScope.{declared.Scope}, which goes to an audience rather than one player, so [Target] means nothing on it.");

        if (declared.Scope == EventScope.Area && declared.Subjects.Count == 0 && declared.AreaMember is null
            || declared.Scope == EventScope.Cell && declared.Subjects.Count == 0 && declared.CellMember is null)
            Report(context, location, "NEVT003", "No audience",
                $"'{declared.Symbol.Name}' is EventScope.{declared.Scope} with no [Subject] to take its audience from. Mark a subject, or an [{declared.Scope}] member naming it.");

        if (declared.Policy == Policy.AreaMaster && declared.AreaMember is null)
            Report(context, location, "NEVT007", "No area to check",
                $"'{declared.Symbol.Name}' says Policy.AreaMaster, which checks who is master of the area the event is in, so one member must be marked [Area] to name it. The sender is not named: the server already knows who sent it.");

        if (declared.Policy == Policy.CellMaster && declared.CellMember is null)
            Report(context, location, "NEVT008", "No cell to check",
                $"'{declared.Symbol.Name}' says Policy.CellMaster, which checks who is master of the cell the event is in, so one member must be marked [Cell] to name it. The sender is not named: the server already knows who sent it.");

        if (declared.AreaMember is { } area && PlaceMemberKindOf(area.Type, AreaId, AreaShape) is null)
            Report(context, area.Locations.FirstOrDefault() ?? location, "NEVT009", "Not a place",
                $"'{declared.Symbol.Name}.{area.Name}' is marked [Area] but is '{area.Type.ToDisplayString()}'. An area is named by an AreaId, or by an Area, which may be absent.");

        if (declared.CellMember is { } cell && PlaceMemberKindOf(cell.Type, FullCellId, CellShape) is null)
            Report(context, cell.Locations.FirstOrDefault() ?? location, "NEVT009", "Not a place",
                $"'{declared.Symbol.Name}.{cell.Name}' is marked [Cell] but is '{cell.Type.ToDisplayString()}'. A cell is named by a FullCellId, or by a Cell, which may be absent.");

        if (PolicyChecksSubject(declared.Policy) && declared.Subjects.Count == 0)
            Report(context, location, "NEVT004", "No subject to check",
                $"'{declared.Symbol.Name}' says Policy.{declared.Policy}, which checks the subject, so one member must be marked [Subject].");

        foreach (var member in declared.Members)
        {
            if (RpcEntityParams.IsShape(member.Type, out var shape, out _))
            {
                if (!RpcEntityParams.CanBeOnTheNetwork(shape))
                    Report(context, member.Locations.FirstOrDefault() ?? location, "NEVT006", "Shape cannot travel",
                        $"'{declared.Symbol.Name}.{member.Name}' is '{shape.Name}', whose entities never have a network id, so it cannot be sent.");
            }
            else if (!IsSerializable(member.Type))
            {
                Report(context, member.Locations.FirstOrDefault() ?? location, "NEVT014", "Member cannot travel",
                    $"'{declared.Symbol.Name}.{member.Name}' is '{member.Type.ToDisplayString()}', which an event has no way to send. A member is a primitive, an enum, a System.Numerics vector, an INetSerializable, or a shape.");

                sendable = false;
            }
        }

        return sendable;
    }

    /// <summary>Whether a member that is not a shape has a way onto the wire, which is what the reader is asked for.</summary>
    private static bool IsSerializable(ITypeSymbol type)
        => type.TypeKind == TypeKind.Enum
           || SerializationHelper.IsVectorLike(type)
           || SerializationHelper.IsINetSerializable(type)
           || SerializationHelper.IsSerializablePrimitive(type.SpecialType);

    /// <summary>
    /// Only the owner check reads the subject. The two master policies read the place the event names, and the
    /// sender they compare it with is the origin, which the server knows without the event saying so.
    /// </summary>
    private static bool PolicyChecksSubject(Policy policy) => policy == Policy.SubjectOwner;

    /// <summary>
    /// How a member names a place: by its id, or by its shape, which may be absent. A nullable id is not one of
    /// them: a shape travels as its entity's id and may be missing, but a nullable id has no way onto the wire.
    /// </summary>
    private enum PlaceMemberKind
    {
        Id,
        Shape,
        OptionalShape,
    }

    /// <summary>How <paramref name="type"/> names the place, or null when it is not a form that can.</summary>
    private static PlaceMemberKind? PlaceMemberKindOf(ITypeSymbol type, string id, string shape)
    {
        var optional = type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };
        var underlying = optional ? ((INamedTypeSymbol)type).TypeArguments[0] : type;
        var name = underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        if (name == id && !optional)
            return PlaceMemberKind.Id;

        if (name == shape)
            return optional ? PlaceMemberKind.OptionalShape : PlaceMemberKind.Shape;

        return null;
    }

    private static void EmitEvent(SourceProductionContext context, DeclaredEvent declared)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using LiteNetLib.Utils;");
        sb.AppendLine();
        sb.AppendLine($"namespace {declared.Symbol.ContainingNamespace.ToDisplayString()};");
        sb.AppendLine();
        sb.AppendLine($"public partial struct {declared.Symbol.Name} : {NetworkedEventInterface}");
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
            else if (SerializationHelper.IsVectorLike(member.Type))
            {
                sb.AppendLine($"        {VectorSerialization}.Serialize({member.Name}, writer);");
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

        context.AddSource($"{declared.Symbol.Name}.NetworkedEvent.g.cs", sb.ToString());
    }

    /// <summary>
    /// Everything about the event that its handlers do not decide: where it goes, who may raise it, and how it
    /// reads back off the wire. One per event rather than one per mod, which is what lets the coordinator that
    /// runs every mod's handlers check the policy once and fan the event out once.
    /// </summary>
    private static void EmitContract(StringBuilder sb, DeclaredEvent declared)
    {
        var name = declared.Symbol.Name;
        var type = declared.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var subject = declared.Subjects.FirstOrDefault();

        sb.AppendLine("/// <exclude/>");
        sb.AppendLine($"/// <summary>What {name} is, for the coordinator that runs its handlers.</summary>");
        sb.AppendLine($"[{EditorBrowsable}]");
        sb.AppendLine($"public sealed class {name}Contract : {ContractInterface}<{type}>");
        sb.AppendLine("{");
        sb.AppendLine("    public global::ReadyM.Api.Multiplayer.Protocol.Enums.RelayMessageCode Code");
        sb.AppendLine($"        => {ManifestClassName}.{name}Code + {ManifestClassName}.Offset;");
        sb.AppendLine();
        sb.AppendLine($"    public {AttributesNamespace}.EventScope Scope => {AttributesNamespace}.EventScope.{declared.Scope};");
        sb.AppendLine();
        sb.AppendLine($"    public {AttributesNamespace}.Policy Policy => {AttributesNamespace}.Policy.{declared.Policy};");
        sb.AppendLine();
        sb.AppendLine($"    public {EntityHandle}? Subject(in {type} networkEvent)");
        sb.AppendLine($"        => {SubjectExpression(subject)};");
        sb.AppendLine();
        sb.AppendLine($"    public global::ReadyM.Api.Idents.PlayerId Target(in {type} networkEvent)");
        sb.AppendLine($"        => {(declared.Target is { } target ? $"networkEvent.{target.Name}.PlayerId" : "global::ReadyM.Api.Idents.PlayerId.Invalid")};");
        sb.AppendLine();

        EmitTryPlace(sb, type, "TryArea", "area", AreaId, AreaShape, "AreaId", declared.AreaMember);
        EmitTryPlace(sb, type, "TryCell", "cell", FullCellId, CellShape, "FullCellId", declared.CellMember);

        EmitTryRead(sb, declared, type);
        EmitReasonSerialization(sb, declared);

        sb.AppendLine("}");
    }

    /// <summary>
    /// The place the event names with <c>[Area]</c> or <c>[Cell]</c>, as its id. The audience is taken from it
    /// before the subject, and the master policies check it. An event that names none answers false.
    /// </summary>
    private static void EmitTryPlace(
        StringBuilder sb, string type, string method, string output, string id, string shape, string idProperty,
        IPropertySymbol? member)
    {
        sb.AppendLine($"    public bool {method}(in {type} networkEvent, out {id} {output})");
        sb.AppendLine("    {");

        switch (member is null ? null : PlaceMemberKindOf(member.Type, id, shape))
        {
            case PlaceMemberKind.Id:
                sb.AppendLine($"        {output} = networkEvent.{member!.Name};");
                sb.AppendLine("        return true;");
                break;

            case PlaceMemberKind.Shape:
                sb.AppendLine($"        {output} = networkEvent.{member!.Name}.{idProperty};");
                sb.AppendLine("        return true;");
                break;

            case PlaceMemberKind.OptionalShape:
                sb.AppendLine($"        if (networkEvent.{member!.Name} is {{ }} named)");
                sb.AppendLine("        {");
                sb.AppendLine($"            {output} = named.{idProperty};");
                sb.AppendLine("            return true;");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine($"        {output} = default;");
                sb.AppendLine("        return false;");
                break;

            case null:
                sb.AppendLine($"        {output} = default;");
                sb.AppendLine("        return false;");
                break;

            default:
                throw new System.ArgumentOutOfRangeException(nameof(member), "Unhandled way of naming a place.");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>The entity the event is about, as a handle the server checks ownership and scope against.</summary>
    private static string SubjectExpression(IPropertySymbol? subject)
    {
        if (subject is null)
            return "null";

        return RpcEntityParams.IsShape(subject.Type, out _, out var optional) && optional
            ? $"networkEvent.{subject.Name} is {{ }} subject ? {EntityHandle}.Of(subject) : null"
            : $"{EntityHandle}.Of(networkEvent.{subject.Name})";
    }

    /// <summary>
    /// Reads the event back in the order it wrote itself. An entity member names an id this end may not have,
    /// so a miss drops the whole event: half an event is worse than none, and the sender's own end has already
    /// been told whether it was accepted.
    /// </summary>
    private static void EmitTryRead(StringBuilder sb, DeclaredEvent declared, string type)
    {
        sb.AppendLine($"    public bool TryRead({EntityReader} entities, NetDataReader reader, out {type} networkEvent)");
        sb.AppendLine("    {");
        sb.AppendLine("        networkEvent = default;");
        sb.AppendLine();

        for (var i = 0; i < declared.Members.Count; i++)
        {
            var member = declared.Members[i];

            if (RpcEntityParams.IsShape(member.Type, out var shape, out var optional))
            {
                var read = optional ? "TryReadOptional" : "TryRead";
                var named = shape.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                sb.AppendLine(
                    $"        if (!entities.{read}<{named}>("
                    + $"reader, out var {Local(member)}, \"{declared.Symbol.Name}\", \"{member.Name}\"))");
                sb.AppendLine("            return false;");
            }
            else if (SerializationHelper.IsVectorLike(member.Type))
            {
                sb.AppendLine($"        var {Local(member)} = default({member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)});");
                sb.AppendLine($"        {VectorSerialization}.Deserialize(ref {Local(member)}, reader);");
            }
            else
            {
                sb.AppendLine($"        var {Local(member)} = {ReadExpression(member.Type)};");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"        networkEvent = new {type}");
        sb.AppendLine("        {");

        for (var i = 0; i < declared.Members.Count; i++)
            sb.AppendLine($"            {declared.Members[i].Name} = {Local(declared.Members[i])},");

        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        return true;");
        sb.AppendLine("    }");
        sb.AppendLine();
    }

    /// <summary>The local a member is read into.</summary>
    private static string Local(IPropertySymbol member) => "read" + member.Name;

    /// <summary>
    /// A refusal's reason, of the type the declaration named. It gets a presence byte because an event may
    /// name a reject type and still be refused without one, by the policy or by a bare Cancel().
    /// </summary>
    private static void EmitReasonSerialization(StringBuilder sb, DeclaredEvent declared)
    {
        sb.AppendLine("    public void WriteReason(NetDataWriter writer, object? reason)");

        if (declared.RejectType is { } reject)
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

    /// <summary>Declares every event this assembly holds. The SDK calls it, with its registry.</summary>
    private static void EmitDeclarations(SourceProductionContext context, List<DeclaredEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine();
        sb.AppendLine("namespace ReadyM.SDK.Generated;");
        sb.AppendLine();
        sb.AppendLine("/// <exclude/>");
        sb.AppendLine("/// <summary>Declares this assembly's networked events.</summary>");
        sb.AppendLine($"[{EditorBrowsable}]");
        sb.AppendLine("public static class NetworkedEventDeclarations");
        sb.AppendLine("{");
        sb.AppendLine($"    public static void DeclareAll({Registry} registry)");
        sb.AppendLine("    {");

        foreach (var declared in events)
        {
            var type = declared.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var contract = $"global::{declared.Symbol.ContainingNamespace.ToDisplayString()}.{declared.Symbol.Name}Contract";

            sb.AppendLine($"        registry.Declare<{type}>(new {contract}());");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        context.AddSource("NetworkedEventDeclarations.g.cs", sb.ToString());
    }

    /// <summary>How one plain member reads itself back, cast where the wire carries the underlying type.</summary>
    private static string ReadExpression(ITypeSymbol type)
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
        SourceProductionContext context, string ns, string manifestId, List<DeclaredEvent> events)
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
            new DiagnosticDescriptor(id, title, message, "NetworkedEvent", DiagnosticSeverity.Error, true),
            location));

    /// <summary>One declared event, with what its attribute and its members said.</summary>
    private sealed class DeclaredEvent
    {
        public DeclaredEvent(INamedTypeSymbol symbol, AttributeData declaration)
        {
            Symbol = symbol;

            Scope = declaration.ConstructorArguments.Length > 0
                ? AttributeUtils.EnumArgument(declaration.ConstructorArguments[0], EventScope.Server)
                : EventScope.Server;

            Policy = declaration.ConstructorArguments.Length > 1
                ? AttributeUtils.EnumArgument(declaration.ConstructorArguments[1], Policy.ServerAuthoritative)
                : Policy.ServerAuthoritative;

            RejectType = declaration.NamedArguments
                .FirstOrDefault(n => n.Key == "Reject").Value.Value as INamedTypeSymbol;

            Members = [.. symbol.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic && p.SetMethod is not null)];
            Subjects = [.. Members.Where(m => HasAttribute(m, "SubjectAttribute"))];
            Target = Members.FirstOrDefault(m => HasAttribute(m, "TargetAttribute"));
            AreaMember = Members.FirstOrDefault(m => HasAttribute(m, "AreaAttribute"));
            CellMember = Members.FirstOrDefault(m => HasAttribute(m, "CellAttribute"));
        }

        public INamedTypeSymbol Symbol { get; }

        public EventScope Scope { get; }

        public Policy Policy { get; }

        /// <summary>The type a refusal carries back, where the declaration named one.</summary>
        public INamedTypeSymbol? RejectType { get; }

        public List<IPropertySymbol> Members { get; }

        public List<IPropertySymbol> Subjects { get; }

        public IPropertySymbol? Target { get; }

        /// <summary>The member marked [Area], naming the area the event is in.</summary>
        public IPropertySymbol? AreaMember { get; }

        /// <summary>The member marked [Cell], naming the cell the event is in.</summary>
        public IPropertySymbol? CellMember { get; }

        private static bool HasAttribute(ISymbol member, string attribute)
            => member.GetAttributes().Any(a => a.AttributeClass?.Name == attribute);
    }

    /// <summary>Mirrors <c>ReadyM.SDK.Attributes.EventScope</c>, which a generator cannot reference.</summary>
    private enum EventScope
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
