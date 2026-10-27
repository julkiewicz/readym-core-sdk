using System.Reflection;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Archetypes;
using Xunit;

namespace ReadyM.Api.Generators.Tests.Archetypes;

/// <summary>
/// A public property written with a body on a mixin reaches whatever includes or extends the mixin, as its values do.
/// Without it, an archetype carried a mixin's values but none of what the mixin works out from them.
/// </summary>
public class ComputedMemberTests(ITestOutputHelper output)
{
    private static readonly Assembly[] Sdk =
    [
        typeof(SDK.Attributes.ArchetypeAttribute).Assembly,
        typeof(SDK.Chunks.ChunkSlot).Assembly,
        typeof(SDK.Server.Entities.IEntities).Assembly,
        typeof(SDK.Client.Entities.IEntities).Assembly,
        typeof(Api.Multiplayer.ECS.Components.INetworkedComponent).Assembly
    ];

    private const string Health =
        """
        using ReadyM.SDK.Attributes;

        namespace Mod;

        [ArchetypeMixin(Replication.None)]
        public readonly partial struct Health
        {
            public partial float Value { get; set; }

            public partial float Max { get; set; }

            public bool IsLow => Value < Max / 4;

            public float Missing
            {
                get => Max - Value;
                set => Value = Max - value;
            }

            private bool Hidden => Value < 0;

            internal bool Internal => Value < 0;

            public static bool Static => true;

            public float Fixed { get => Value; init { } }
        }
        """;

    private static string Source(string shapes) =>
        $$"""
          using ReadyM.SDK.Attributes;

          namespace Mod;

          {{shapes}}
          """;

    [Fact]
    public void An_archetype_gets_the_computed_properties_of_a_mixin_it_includes()
    {
        var run = Run(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            public readonly partial struct Hero;

            public static class Use
            {
                public static bool Low(Hero hero) => hero.IsLow;

                public static void Heal(Hero hero) => hero.Missing = 0;
            }
            """);

        Assert.Empty(Errors(run));
    }

    /// <summary>A loop over a shape on the server hands over its chunk view, which needs them as much as the shape.</summary>
    [Fact]
    public void A_query_loop_reads_them_off_the_chunk_view()
    {
        var run = Run(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            public readonly partial struct Hero;

            public static class Use
            {
                public static int Low(ReadyM.SDK.Server.Entities.IEntities entities)
                {
                    var low = 0;

                    foreach (var hero in entities.Query<Hero>())
                        if (hero.IsLow && hero.Missing > 0)
                            low++;

                    foreach (var health in entities.Query<Health>())
                        if (health.IsLow)
                            low++;

                    return low;
                }
            }
            """);

        Assert.Empty(Errors(run));
    }

    [Fact]
    public void A_prefix_renames_them_as_it_renames_values()
    {
        var run = Run(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health), "Main")]
            public readonly partial struct Hero;

            public static class Use
            {
                public static bool Low(Hero hero) => hero.MainIsLow && hero.MainMissing > 0;
            }
            """);

        Assert.Empty(Errors(run));
    }

    [Fact]
    public void They_reach_through_an_included_archetype()
    {
        var run = Run(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            public readonly partial struct Walker;

            [Archetype(Networked = false)]
            [IncludeArchetype(typeof(Walker))]
            public readonly partial struct Hero;

            public static class Use
            {
                public static bool Low(Hero hero) => hero.IsLow;
            }
            """);

        Assert.Empty(Errors(run));
    }

    [Fact]
    public void An_extended_archetype_gets_them_with_a_setter_method()
    {
        var run = Run(
            """
            [Archetype(Networked = false)]
            public readonly partial struct Walker;

            [ArchetypeMixin(Replication.None)]
            [Extends(typeof(Walker))]
            public readonly partial struct Stamina
            {
                public partial float Value { get; set; }

                public bool IsTired => Value < 10;

                public float Spent
                {
                    get => 100 - Value;
                    set => Value = 100 - value;
                }
            }

            public static class Use
            {
                public static bool Tired(Walker walker) => walker.IsTired && walker.Spent > 0;

                public static void Rest(Walker walker) => walker.SetSpent(0);
            }
            """);

        Assert.Empty(Errors(run));
    }

    [Fact]
    public void Only_public_instance_properties_with_a_public_getter_are_passed_on()
    {
        var run = Run(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            public readonly partial struct Hero;
            """);

        var generated = string.Concat(run.GeneratedSyntaxTrees.Select(tree => tree.ToString()));

        Assert.Contains("Computed.IsLow(_handle)", generated);
        Assert.DoesNotContain("Computed.Hidden", generated);
        Assert.DoesNotContain("Computed.Internal", generated);
        Assert.DoesNotContain("Computed.Static", generated);
        Assert.DoesNotContain("SetFixed", generated);
        Assert.Empty(Errors(run));
    }

    [Fact]
    public void A_computed_property_and_a_value_of_one_name_clash()
    {
        var run = Run(
            """
            [ArchetypeMixin(Replication.None)]
            public readonly partial struct Alarm
            {
                public partial bool IsLow { get; set; }
            }

            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            [Include(typeof(Alarm))]
            public readonly partial struct Hero;
            """);

        var clash = Assert.Single(run.DriverRunResult.Diagnostics, d => d.Id == "READYM041");
        Assert.Contains("'IsLow'", clash.GetMessage());
    }

    /// <summary>A mixin from another assembly is read from metadata, where its computed properties are listed for it.</summary>
    [Fact]
    public void A_mixin_compiled_into_another_assembly_passes_them_on()
    {
        var library = SourceGeneratorTestHelper.RunGenerators(
            [("Health.cs", Health)],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator()],
            output,
            Sdk,
            assemblyName: "Mod.Common");

        Assert.Empty(Errors(library));

        using var image = new MemoryStream();
        Assert.True(library.OutputCompilation.Emit(image).Success);

        var run = SourceGeneratorTestHelper.RunGenerators(
            [("Shapes.cs", Source(
                """
                [Archetype(Networked = false)]
                [Include(typeof(Health))]
                public readonly partial struct Hero;

                public static class Use
                {
                    public static bool Low(Hero hero) => hero.IsLow && hero.Missing > 0;

                    public static void Heal(Hero hero) => hero.Missing = 0;
                }
                """))],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator()],
            output,
            Sdk,
            assemblyName: "Mod.Client",
            alsoReferenceMetadata: [MetadataReference.CreateFromImage(image.ToArray())]);

        Assert.Empty(Errors(run));
    }

    private static Diagnostic[] Errors(SourceGeneratorTestHelper.GeneratorRunResult run)
        => [.. run.OutputDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)];

    private SourceGeneratorTestHelper.GeneratorRunResult Run(string shapes)
        => SourceGeneratorTestHelper.RunGenerators(
            [("Health.cs", Health), ("Shapes.cs", Source(shapes))],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator(), new RegistrationAggregatorGenerator()],
            output,
            Sdk,
            SourceGeneratorTestHelper.SdkInternalsAssembly);
}
