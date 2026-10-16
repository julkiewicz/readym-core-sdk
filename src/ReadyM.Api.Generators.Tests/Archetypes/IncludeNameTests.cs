using System.Reflection;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Archetypes;
using Xunit;

namespace ReadyM.Api.Generators.Tests.Archetypes;

/// <summary>
/// Two members a shape would expose under one name are refused, whichever pair they come from. Keeping only the first,
/// as the generator used to, sent every call to whichever include happened to be listed first.
/// </summary>
public class IncludeNameTests(ITestOutputHelper output)
{
    private static readonly Assembly[] Sdk =
    [
        typeof(SDK.Attributes.ArchetypeAttribute).Assembly,
        typeof(SDK.Chunks.ChunkSlot).Assembly,
        typeof(SDK.Server.Entities.IEntities).Assembly,
        typeof(SDK.Client.Entities.IEntities).Assembly,
        typeof(Api.Multiplayer.ECS.Components.INetworkedComponent).Assembly
    ];

    // Two components with the same collection surface, as three of Oblivion's attribute components have
    private const string Core =
        """
        using Friflo.Engine.ECS;

        namespace Core;

        public struct SkillsComponent : IComponent
        {
            public int GetAttribute(byte key) => 0;
            public void SetAttribute(byte key, int value) { }
        }

        public struct EffectsComponent : IComponent
        {
            public int GetAttribute(byte key) => 0;
            public void SetAttribute(byte key, int value) { }
        }
        """;

    private const string Mixins =
        """
        [ArchetypeMixin(Replication.None)]
        public readonly partial struct Health
        {
            public partial float Value { get; set; }
        }

        [ArchetypeMixin(Replication.None)]
        public readonly partial struct Mana
        {
            public partial float Value { get; set; }
        }

        [ArchetypeMixin(Replication.None)]
        [ExplicitComponent(typeof(Core.SkillsComponent))]
        [ExplicitCollection("Attribute")]
        public readonly partial struct Skills;

        [ArchetypeMixin(Replication.None)]
        [ExplicitComponent(typeof(Core.EffectsComponent))]
        [ExplicitCollection("Attribute")]
        public readonly partial struct Effects;
        """;

    private static string Source(string shapes) =>
        $$"""
          using ReadyM.SDK.Attributes;

          namespace Mod;

          {{Mixins}}

          {{shapes}}
          """;

    [Fact]
    public void Two_mixins_with_a_property_of_the_same_name_are_refused()
    {
        var error = Assert.Single(Report(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            [Include(typeof(Mana))]
            public readonly partial struct Hero;
            """), d => d.Id == "READYM041");

        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'Hero'", error.GetMessage());
        Assert.Contains("'Value'", error.GetMessage());
        Assert.Contains("Health", error.GetMessage());
        Assert.Contains("Mana", error.GetMessage());
    }

    [Fact]
    public void A_prefix_keeps_two_properties_of_the_same_name_apart()
    {
        const string shapes =
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            [Include(typeof(Mana), "Mana")]
            public readonly partial struct Hero;
            """;

        Assert.DoesNotContain(Report(shapes), d => d.Id == "READYM041");
        Assert.DoesNotContain(Run(shapes).OutputDiagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("ManaValue", Generated(shapes));
    }

    [Fact]
    public void Two_mixins_with_a_collection_of_the_same_name_are_refused()
    {
        var reported = Report(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Skills))]
            [Include(typeof(Effects))]
            public readonly partial struct Hero;
            """);

        var errors = reported.Where(d => d.Id == "READYM041").ToArray();
        Assert.Contains(errors, d => d.GetMessage().Contains("'GetAttribute'"));
        Assert.Contains(errors, d => d.GetMessage().Contains("'SetAttribute'"));
    }

    [Fact]
    public void A_prefix_keeps_two_collections_of_the_same_name_apart()
    {
        const string shapes =
            """
            [Archetype(Networked = false)]
            [Include(typeof(Skills), "Skill")]
            [Include(typeof(Effects), "Effect")]
            public readonly partial struct Hero;
            """;

        Assert.DoesNotContain(Report(shapes), d => d.Id == "READYM041");
        Assert.DoesNotContain(Run(shapes).OutputDiagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var generated = Generated(shapes);
        Assert.Contains("GetSkillAttribute", generated);
        Assert.Contains("GetEffectAttribute", generated);
    }

    [Fact]
    public void A_mixin_property_named_like_the_archetypes_own_is_refused()
    {
        var error = Assert.Single(Report(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            public readonly partial struct Hero
            {
                public partial float Value { get; set; }
            }
            """), d => d.Id == "READYM041");

        Assert.Contains("'Value'", error.GetMessage());
        Assert.Contains("Health", error.GetMessage());
    }

    // Archetypes are entity types and building on one is inheritance, so the attribute allows one. Mixins compose.
    [Fact]
    public void Building_on_two_archetypes_does_not_compile()
    {
        var input = Run(
            """
            [Archetype(Networked = false)]
            public readonly partial struct Walker;

            [Archetype(Networked = false)]
            public readonly partial struct Caster;

            [Archetype(Networked = false)]
            [IncludeArchetype(typeof(Walker))]
            [IncludeArchetype(typeof(Caster))]
            public readonly partial struct Hero;
            """).InputDiagnostics;

        Assert.Contains(input, d => d.Id == "CS0579");
    }

    [Fact]
    public void An_archetype_named_by_include_is_refused()
    {
        var error = Assert.Single(Report(
            """
            [Archetype(Networked = false)]
            public readonly partial struct Walker;

            [Archetype(Networked = false)]
            [Include(typeof(Walker))]
            public readonly partial struct Hero;
            """), d => d.Id == "READYM042");

        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("'Hero'", error.GetMessage());
        Assert.Contains("[IncludeArchetype(typeof(Walker))]", error.GetMessage());
    }

    [Fact]
    public void A_mixin_named_by_include_archetype_is_refused()
    {
        var error = Assert.Single(Report(
            """
            [Archetype(Networked = false)]
            [IncludeArchetype(typeof(Health))]
            public readonly partial struct Hero;
            """), d => d.Id == "READYM043");

        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Contains("[Include(typeof(Health))]", error.GetMessage());
    }

    [Fact]
    public void One_archetype_and_any_number_of_mixins_are_fine()
    {
        var reported = Report(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            public readonly partial struct Walker;

            [Archetype(Networked = false)]
            [IncludeArchetype(typeof(Walker))]
            [Include(typeof(Mana), "Mana")]
            [Include(typeof(Skills))]
            public readonly partial struct Hero;
            """);

        Assert.DoesNotContain(reported, d => d.Id is "READYM041" or "READYM042");
    }

    // Redundant, but one member reached twice rather than two members of one name
    [Fact]
    public void A_mixin_the_included_archetype_already_has_is_not_a_clash()
    {
        var reported = Report(
            """
            [Archetype(Networked = false)]
            [Include(typeof(Health))]
            public readonly partial struct Walker;

            [Archetype(Networked = false)]
            [IncludeArchetype(typeof(Walker))]
            [Include(typeof(Health))]
            public readonly partial struct Hero;
            """);

        Assert.DoesNotContain(reported, d => d.Id == "READYM041");
    }

    private Diagnostic[] Report(string shapes)
        => [.. Run(shapes).DriverRunResult.Diagnostics.Where(d => d.Id.StartsWith("READYM"))];

    private string Generated(string shapes)
        => string.Concat(Run(shapes).GeneratedSyntaxTrees.Select(tree => tree.ToString()));

    private SourceGeneratorTestHelper.GeneratorRunResult Run(string shapes)
        => SourceGeneratorTestHelper.RunGenerators(
            [("Core.cs", Core), ("Shapes.cs", Source(shapes))],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator()],
            output,
            Sdk,
            SourceGeneratorTestHelper.SdkInternalsAssembly);

    /// <summary>A value an include brings shows the docs it has where it is declared, whatever it is called here.</summary>
    [Fact]
    public void An_included_value_inherits_the_docs_of_where_it_is_declared()
    {
        const string shapes =
            """
            [Archetype(Networked = false)]
            public readonly partial struct Walker
            {
                public partial int Steps { get; set; }
            }

            [Archetype(Networked = false)]
            [IncludeArchetype(typeof(Walker))]
            [Include(typeof(Health))]
            [Include(typeof(Mana), "Mana")]
            public readonly partial struct Hero;
            """;

        var generated = Generated(shapes);

        Assert.Contains("/// <inheritdoc cref=\"global::Mod.Health.Value\"/>", generated);
        Assert.Contains("/// <inheritdoc cref=\"global::Mod.Mana.Value\"/>", generated);
        Assert.Contains("/// <inheritdoc cref=\"global::Mod.Walker.Steps\"/>", generated);
        Assert.DoesNotContain(Run(shapes).OutputDiagnostics, d => d.Id == "CS1574");
    }
}
