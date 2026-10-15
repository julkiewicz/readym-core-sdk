using System.Reflection;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Archetypes;
using Xunit;

namespace ReadyM.Api.Generators.Tests.Archetypes;

/// What a shape says about persisting, and what the generator refuses, run against the real SDK.
public class PersistenceTests(ITestOutputHelper output)
{
    private static readonly Assembly[] Sdk =
    [
        typeof(SDK.Attributes.ArchetypeAttribute).Assembly,
        typeof(SDK.Chunks.ChunkSlot).Assembly,
        typeof(SDK.Server.Entities.IEntities).Assembly,
        typeof(SDK.Client.Entities.IEntities).Assembly,
        typeof(Api.Multiplayer.ECS.Components.INetworkedComponent).Assembly
    ];

    private const string Core =
        """
        using Friflo.Engine.ECS;

        namespace Core;

        public struct LocalComponent : IComponent
        {
            public int Value;
        }
        """;

    private static string Source(string shape) =>
        $$"""
          using ReadyM.SDK.Attributes;

          namespace Mod;

          {{shape}}
          """;

    private Diagnostic[] Report(string shape)
    {
        var result = SourceGeneratorTestHelper.RunGenerators(
            [("Core.cs", Core), ("Shape.cs", Source(shape))],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator()],
            output,
            Sdk,
            SourceGeneratorTestHelper.SdkInternalsAssembly);

        return [.. result.DriverRunResult.Diagnostics.Where(d => d.Id.StartsWith("READYM"))];
    }

    [Fact]
    public void A_shape_with_values_persists_without_complaint()
    {
        var reported = Report("""
            [ArchetypeMixin]
            [Persisted("Mod.Subject")]
            public readonly partial struct Subject
            {
                public partial int Value { get; set; }
            }
            """);

        Assert.DoesNotContain(reported, d => d.Id is "READYM041" or "READYM042" or "READYM043");
    }

    [Fact]
    public void A_marker_shape_cannot_persist()
    {
        var reported = Report("""
            [ArchetypeMixin]
            [Persisted("Mod.Subject")]
            public readonly partial struct Subject;
            """);

        Assert.Contains(reported, d => d.Id == "READYM042" && d.Severity == DiagnosticSeverity.Error);
    }

    /// A borrowed component decides for itself whether it saves.
    [Fact]
    public void A_shape_cannot_persist_a_borrowed_component()
    {
        var reported = Report("""
            [ArchetypeMixin]
            [Persisted("Mod.Subject")]
            [ExplicitComponent(typeof(global::Core.LocalComponent))]
            public readonly partial struct Subject
            {
                public partial int Value { get; set; }
            }
            """);

        Assert.Contains(reported, d => d.Id == "READYM041" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void A_persisted_shape_needs_a_save_name()
    {
        var reported = Report("""
            [ArchetypeMixin]
            [Persisted(" ")]
            public readonly partial struct Subject
            {
                public partial int Value { get; set; }
            }
            """);

        Assert.Contains(reported, d => d.Id == "READYM043" && d.Severity == DiagnosticSeverity.Error);
    }
}
