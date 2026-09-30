using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReadyM.Api.Generators.Derive.GameEvents;

namespace ReadyM.Api.Generators;

/// <summary>
/// Generates the <c>IGameEvent</c> methods of an event marked <c>[DeriveIGameEvent]</c>, with the policy its
/// discriminator attribute chooses (<c>[OwnershipBased]</c> and the rest of <see cref="GameEventSupportRegistry"/>).
/// An event without <c>[DeriveIGameEvent]</c> writes the methods by hand.
/// </summary>
[Generator]
internal sealed class DeriveIGameEventGenerator : IIncrementalGenerator
{
    private const string AttributeNamespace = "ReadyM.Api.Mapping.Events";
    private const string DeriveAttributeName = "DeriveIGameEventAttribute";
    private const string EntityType = "Friflo.Engine.ECS.Entity";
    private const string RawEntityType = "Friflo.Engine.ECS.RawEntity";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .CreateSyntaxProvider(Predicate, Transform)
            .Where(m => m is not null);

        context.RegisterSourceOutput(models, (spc, model) =>
        {
            var name = model!.Event.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", "");
            spc.AddSource($"{name}.GameEvent.g.cs", Emit(model));
        });
    }

    private static bool Predicate(SyntaxNode node, CancellationToken _)
        => node is StructDeclarationSyntax { AttributeLists.Count: > 0 };

    private static bool IsDerive(AttributeData attribute)
        => attribute.AttributeClass is { } type
           && type.ContainingNamespace.ToDisplayString() == AttributeNamespace
           && type.Name == DeriveAttributeName;

    private static bool IsDiscriminator(AttributeData attribute)
        => attribute.AttributeClass is { } type
           && type.ContainingNamespace.ToDisplayString() == AttributeNamespace
           && GameEventSupportRegistry.DiscriminatorNames.Contains(type.Name);

    private static GameEventModel? Transform(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var node = (StructDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(node, ct) is not INamedTypeSymbol symbol)
            return null;

        var attributes = symbol.GetAttributes();
        var derive = attributes.FirstOrDefault(IsDerive);
        var discriminators = attributes.Where(IsDiscriminator).ToArray();
        if (derive == null && discriminators.Length == 0)
            return null;

        // NOTE: Each part of a partial struct is its own syntax node, and each would emit. Only the part carrying
        // [DeriveIGameEvent] emits; without it, the part carrying the discriminator does, so the error appears once.
        var attributeSyntax = (derive ?? discriminators[0]).ApplicationSyntaxReference?.GetSyntax(ct);
        if (attributeSyntax == null || !attributeSyntax.Ancestors().Contains(node))
            return null;

        var errors = new List<string>();
        var display = symbol.ToDisplayString();

        if (derive == null)
        {
            var name = discriminators[0].AttributeClass!.Name;
            errors.Add($"{display} has a discriminator ({name}) but no [DeriveIGameEvent]; a discriminator only chooses the policy [DeriveIGameEvent] generates.");
            return new GameEventModel(symbol, discriminators[0], null, errors);
        }

        if (discriminators.Length != 1)
        {
            var names = discriminators.Length == 0 ? "none" : string.Join(", ", discriminators.Select(d => d.AttributeClass!.Name));
            errors.Add($"{display} has [DeriveIGameEvent] and {discriminators.Length} discriminators ({names}); a generated game event takes exactly one discriminator.");
            if (discriminators.Length == 0)
                return new GameEventModel(symbol, derive, null, errors);
        }

        if (symbol.ContainingType != null)
            errors.Add($"{display} is nested in {symbol.ContainingType.ToDisplayString()}; a game event must be a top-level struct.");

        var discriminator = discriminators[0];
        IFieldSymbol? subject = null;
        if (GameEventSupportRegistry.NamesNeedingSubject.Contains(discriminator.AttributeClass!.Name))
            subject = ResolveSubject(symbol, discriminator, display, errors);

        return new GameEventModel(symbol, discriminator, subject, errors);
    }

    private static IFieldSymbol? ResolveSubject(INamedTypeSymbol symbol, AttributeData discriminator, string display, List<string> errors)
    {
        var name = discriminator.ConstructorArguments.Length == 1 ? discriminator.ConstructorArguments[0].Value as string : null;
        if (string.IsNullOrEmpty(name))
        {
            errors.Add($"{display}: [{discriminator.AttributeClass!.Name}] needs the subject field's name, written with nameof.");
            return null;
        }

        var field = symbol.GetMembers(name!).OfType<IFieldSymbol>().FirstOrDefault(f => !f.IsStatic);
        if (field == null)
        {
            errors.Add($"{display}: the subject '{name}' is not an instance field of the event.");
            return null;
        }

        var type = field.Type.ToDisplayString();
        if (type != EntityType && type != RawEntityType)
        {
            errors.Add($"{display}: the subject '{name}' is a {type}; a subject must be an Entity or RawEntity.");
            return null;
        }

        return field;
    }

    private static string Emit(GameEventModel model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");

        if (model.Errors.Count > 0)
        {
            foreach (var error in model.Errors)
                sb.AppendLine($"#error Game event {error}");
            return sb.ToString();
        }

        var impl = GameEventSupportRegistry.SupportVisitor.GetImpl(model, fallback: false);
        var context = new CSharpEmitGameEventContext(sb, model);
        var ns = model.Event.ContainingNamespace;
        if (!ns.IsGlobalNamespace)
            sb.AppendLine($"namespace {ns.ToDisplayString()};").AppendLine();

        var readOnly = model.Event.IsReadOnly ? "readonly " : "";
        const string events = "global::ReadyM.Api.Mapping.Events";
        const string parameter = $"{events}.GameEventContextRegistry {CSharpEmitGameEventContext.ContextsParameter}";

        sb.AppendLine($"{readOnly}partial struct {model.Event.Name} : {events}.IGameEvent");
        sb.AppendLine("{");

        sb.AppendLine($"    public {events}.GameEventNotifyResult CanGameEventNotifyEcs({parameter})");
        sb.Append("        => ");
        impl.EmitCanGameEventNotifyEcsBody(context);
        sb.AppendLine(";").AppendLine();

        sb.AppendLine($"    public {events}.GameEventResult CanGameEventRunLocally({parameter})");
        sb.Append("        => ");
        impl.EmitCanGameEventRunLocallyBody(context);
        sb.AppendLine(";").AppendLine();

        sb.AppendLine($"    public {events}.GameEventResult CanEcsInvokeGameEvent({parameter})");
        sb.Append("        => ");
        impl.EmitCanEcsInvokeGameEventBody(context);
        sb.AppendLine(";");

        sb.AppendLine("}");
        return sb.ToString();
    }
}
