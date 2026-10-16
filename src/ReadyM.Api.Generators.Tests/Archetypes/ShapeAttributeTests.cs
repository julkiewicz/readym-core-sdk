using System.Reflection;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Archetypes;
using Xunit;

namespace ReadyM.Api.Generators.Tests.Archetypes;

/// <summary>
/// [Index] once per shape, [Tag] on archetypes and [Extends] on mixins. Each used to get through: a second [Index]
/// compiled and was not the index, a mixin's [Tag] broke the build inside generated code, and an archetype's [Extends]
/// merged its components into the target without its values.
/// </summary>
public class ShapeAttributeTests(ITestOutputHelper output)
{
    private static readonly Assembly[] Sdk =
    [
        typeof(SDK.Attributes.ArchetypeAttribute).Assembly,
        typeof(SDK.Chunks.ChunkSlot).Assembly,
        typeof(SDK.Server.Entities.IEntities).Assembly,
        typeof(SDK.Client.Entities.IEntities).Assembly,
        typeof(Api.Multiplayer.ECS.Components.INetworkedComponent).Assembly
    ];

    private const string Common =
        """
        public struct Watched : Friflo.Engine.ECS.ITag;

        [Archetype(Networked = false)]
        public readonly partial struct Walker;
        """;

    private static string Source(string shapes) =>
        $$"""
          using ReadyM.SDK.Attributes;

          namespace Mod;

          {{Common}}

          {{shapes}}
          """;

    [Fact]
    public void A_second_index_on_a_shape_is_refused()
    {
        var error = Assert.Single(Report(
            """
            [ArchetypeMixin(Replication.None)]
            public readonly partial struct Ticketed
            {
                [Index]
                public partial int Ticket { get; set; }

                [Index]
                public partial int Seat { get; set; }
            }
            """), d => d.Id == "READYM047");

        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'Ticketed'", error.GetMessage());
        Assert.Contains("'Ticket' and 'Seat'", error.GetMessage());
    }

    [Fact]
    public void A_second_index_of_another_type_is_refused_without_errors_in_generated_code()
    {
        var run = Run(
            """
            [ArchetypeMixin(Replication.None)]
            public readonly partial struct Ticketed
            {
                [Index]
                public partial int Ticket { get; set; }

                [Index]
                public partial string Holder { get; set; }
            }
            """);

        Assert.Single(Reported(run), d => d.Id == "READYM047");
        Assert.Empty(GeneratedErrors(run));
    }

    [Fact]
    public void One_index_is_fine()
    {
        var reported = Report(
            """
            [ArchetypeMixin(Replication.None)]
            public readonly partial struct Ticketed
            {
                [Index]
                public partial int Ticket { get; set; }

                public partial int Seat { get; set; }
            }
            """);

        Assert.DoesNotContain(reported, d => d.Id == "READYM047");
    }

    [Fact]
    public void A_tag_on_a_mixin_is_refused_without_errors_in_generated_code()
    {
        var run = Run(
            """
            [ArchetypeMixin(Replication.None)]
            [Tag(typeof(Watched))]
            public readonly partial struct Health
            {
                public partial float Value { get; set; }
            }
            """);

        var error = Assert.Single(Reported(run), d => d.Id == "READYM048");
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'Health'", error.GetMessage());
        Assert.Contains("[Tag(typeof(Watched))]", error.GetMessage());
        Assert.Empty(GeneratedErrors(run));
    }

    [Fact]
    public void A_tag_on_an_archetype_is_fine()
    {
        var reported = Report(
            """
            [Archetype(Networked = false)]
            [Tag(typeof(Watched))]
            public readonly partial struct Hero;
            """);

        Assert.DoesNotContain(reported, d => d.Id == "READYM048");
    }

    [Fact]
    public void Extends_on_an_archetype_is_refused_without_errors_in_generated_code()
    {
        var run = Run(
            """
            [Archetype(Networked = false)]
            [Extends(typeof(Walker))]
            public readonly partial struct Hero;
            """);

        var error = Assert.Single(Reported(run), d => d.Id == "READYM049");
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'Hero'", error.GetMessage());
        Assert.Contains("[Extends(typeof(Walker))]", error.GetMessage());
        Assert.Empty(GeneratedErrors(run));
    }

    [Fact]
    public void Extends_on_a_mixin_is_fine()
    {
        var reported = Report(
            """
            [ArchetypeMixin(Replication.None)]
            [Extends(typeof(Walker))]
            public readonly partial struct Health
            {
                public partial float Value { get; set; }
            }
            """);

        Assert.DoesNotContain(reported, d => d.Id == "READYM049");
    }

    private Diagnostic[] Report(string shapes) => Reported(Run(shapes));

    private static Diagnostic[] Reported(SourceGeneratorTestHelper.GeneratorRunResult run)
        => [.. run.DriverRunResult.Diagnostics.Where(d => d.Id.StartsWith("READYM"))];

    /// <summary>Compiler errors inside generated code, which the diagnostic is there to replace.</summary>
    private static Diagnostic[] GeneratedErrors(SourceGeneratorTestHelper.GeneratorRunResult run)
    {
        var generated = run.GeneratedSyntaxTrees.ToHashSet();

        return
        [
            .. run.OutputDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error
                                                && d.Location.SourceTree is { } tree
                                                && generated.Contains(tree))
        ];
    }

    private SourceGeneratorTestHelper.GeneratorRunResult Run(string shapes)
        => SourceGeneratorTestHelper.RunGenerators(
            [("Shapes.cs", Source(shapes))],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator(), new RegistrationAggregatorGenerator()],
            output,
            Sdk,
            SourceGeneratorTestHelper.SdkInternalsAssembly);
}
