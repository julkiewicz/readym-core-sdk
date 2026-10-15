using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ReadyM.Api.Generators.Derive.CSharp;
using ReadyM.Api.Generators.Derive.CSharp.Serialization;

namespace ReadyM.Api.Generators;

/// <summary>
/// Emits <c>WriteSave</c>/<c>ReadSave</c> for a struct marked <c>[DeriveSavableComponent]</c> (a component) or
/// <c>[DeriveSaveSerializable]</c> (a value type).
/// </summary>
[Generator]
[SuppressMessage("MicrosoftCodeAnalysisDesign", "RS1032:Define diagnostic message correctly")]
internal sealed class DeriveSavableComponentGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var codeProvider = context.SyntaxProvider.CreateSyntaxProvider(Predicate, Transform);

        context.RegisterSourceOutput(codeProvider, (spc, result) =>
        {
            if (result.Exception is not null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        id: "DSC001",
                        title: $"{nameof(DeriveSavableComponentGenerator)} generator failed",
                        messageFormat: "Save serializer generation failed: {0}",
                        category: "Generator",
                        defaultSeverity: DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    Location.None,
                    string.Join("\\n", result.Exception.ToString().Split(["\r\n", "\r", "\n"], StringSplitOptions.None))));

                return;
            }

            if (string.IsNullOrEmpty(result.GenName))
                return;

            spc.AddSource($"{result.GenName}.Save.g.cs", result.GenCode);
        });
    }

    private bool Predicate(SyntaxNode syntaxNode, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return false;

        if (syntaxNode is not StructDeclarationSyntax { AttributeLists.Count: > 0 } structDecl)
            return false;

        return structDecl.AttributeLists
            .SelectMany(static x => x.Attributes)
            .Any(x => x.Name is IdentifierNameSyntax
            {
                Identifier.Text: "DeriveSavableComponent" or "DeriveSavableComponentAttribute"
                              or "DeriveSaveSerializable" or "DeriveSaveSerializableAttribute"
            });
    }

    private TransformResult Transform(GeneratorSyntaxContext context, CancellationToken ct)
    {
        try
        {
            if (ct.IsCancellationRequested)
                return default;

            var symbol = DeriveUtils.GetTargetSymbol(context, ct);
            var model = DeriveComponentUtils.GetTargetModel(false, symbol, context);
            var isComponent = AttributeUtils.HasAttribute(symbol, "DeriveSavableComponentAttribute");
            var code = isComponent
                ? SaveComponentEmitter.Emit(model, ResolveSaveName(symbol), ResolveSaveVersion(symbol))
                : SaveComponentEmitter.EmitSerializable(model);
            var name = DeriveUtils.GetGeneratedFileName(symbol);

            return TransformResult.Success(name, code);
        }
        catch (Exception ex)
        {
            return TransformResult.FatalError(ex);
        }
    }

    // The [SaveName] override baked at generation time, else the type name.
    private static string ResolveSaveName(ISymbol symbol)
    {
        var attr = symbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.Name is "SaveName" or "SaveNameAttribute");

        if (attr is { ConstructorArguments.Length: > 0 } && attr.ConstructorArguments[0].Value is string name && name.Length > 0)
            return name;

        return symbol.Name;
    }

    // The [SaveVersion] override baked at generation time, else 1.
    private static ushort ResolveSaveVersion(ISymbol symbol)
    {
        var attr = symbol.GetAttributes().FirstOrDefault(a =>
            a.AttributeClass?.Name is "SaveVersion" or "SaveVersionAttribute");

        if (attr is { ConstructorArguments.Length: > 0 } && attr.ConstructorArguments[0].Value is ushort version)
            return version;

        return 1;
    }

    private struct TransformResult(string genName, string genCode, Exception? exception)
    {
        public string GenName { get; } = genName;
        public string GenCode { get; } = genCode;
        public Exception? Exception { get; } = exception;

        public static TransformResult Success(string genName, string genCode)
            => new(genName, genCode, null);
        public static TransformResult FatalError(Exception? exception = null)
            => new(string.Empty, string.Empty, exception);
    }
}
