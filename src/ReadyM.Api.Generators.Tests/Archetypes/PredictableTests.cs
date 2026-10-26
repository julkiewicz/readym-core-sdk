using System.Reflection;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Archetypes;
using Xunit;

namespace ReadyM.Api.Generators.Tests.Archetypes;

/// <summary>
/// [Predictable] on a value, and what the generator owes it: somewhere to hold a client's guess, a getter that
/// reads it in front of the server's value, a setter that puts it there, and a mark on every projection of the
/// value so the analyzer can tell a guess from an ordinary write.
/// </summary>
public class PredictableTests(ITestOutputHelper output)
{
    private static readonly Assembly[] Sdk =
    [
        typeof(SDK.Attributes.ArchetypeAttribute).Assembly,
        typeof(SDK.Chunks.ChunkSlot).Assembly,
        typeof(SDK.Server.Entities.IEntities).Assembly,
        typeof(SDK.Client.Entities.IEntities).Assembly,
        typeof(Api.Multiplayer.ECS.Components.INetworkedComponent).Assembly
    ];

    private const string Guesses = "global::ReadyM.SDK.Events.GuessedValueRegistry";

    private const string Mark = "[global::ReadyM.SDK.Attributes.Predictable]";

    private const string Shapes =
        """
        [ArchetypeMixin(Replication.ServerAuthoritative)]
        public readonly partial struct Curse
        {
            [Predictable]
            public partial bool Active { get; set; }

            public partial int Turns { get; set; }
        }

        [Archetype(Replication = Replication.ServerAuthoritative)]
        [Include(typeof(Curse), "Curse")]
        public readonly partial struct Victim
        {
            public partial int Name { get; set; }
        }
        """;

    /// <summary>One registry per member, so a read of a value nobody guessed at is a count and nothing else.</summary>
    [Fact]
    public void A_guessable_value_gets_somewhere_to_hold_the_guess()
    {
        var generated = Generated(Run(Shapes));

        Assert.Contains($"private static readonly {Guesses}<bool> ActiveGuesses = new();", generated);
        Assert.DoesNotContain("TurnsGuesses", generated);
    }

    /// <summary>
    /// The guess in front of the server's value, which is what lets the code reading it never learn one was
    /// involved. A getter that read the component first would show the guess to nobody.
    /// </summary>
    [Fact]
    public void The_getter_reads_the_guess_before_the_value()
        => Assert.Contains(
            "=> ActiveGuesses.TryGet(handle, out var guessed) ? guessed :", Generated(Run(Shapes)));

    /// <summary>
    /// Nothing reaches the component while a guess is being made, which is the whole of why a client is
    /// allowed to write a value the server owns.
    /// </summary>
    [Fact]
    public void The_setter_holds_the_guess_instead_of_writing_it()
    {
        var generated = Generated(Run(Shapes));

        Assert.Contains("if (ActiveGuesses.Guessing(handle, value))", generated);
        Assert.Contains("    return;", generated);
    }

    /// <summary>A read that cannot see the guess is a wrong value, so every read of it merges.</summary>
    [Fact]
    public void Every_way_of_reading_it_merges_the_guess()
    {
        var generated = Generated(Run(Shapes));

        Assert.Contains("if (ActiveGuesses.TryGet(handle, out value))", generated);
        Assert.Contains("ActiveGuesses.TryGet(handle, out var guessed) ? guessed : chunk.As<", generated);
    }

    /// <summary>
    /// A chunk row is not an entity and a guess is held by one, so the row's handle has to travel with the
    /// read. Only a guessable value asks for it; nothing else is handed something it would not look at.
    /// </summary>
    [Fact]
    public void A_chunk_read_of_it_is_handed_the_row_it_sits_on()
    {
        var generated = Generated(Run(Shapes));

        Assert.Contains("GetActive(in global::ReadyM.SDK.Chunks.ComponentChunk chunk, int index, in ", generated);
        Assert.Contains(Calls(generated, "GetActive("), call => call.EndsWith(", _index, Handle);"));
        Assert.DoesNotContain(Calls(generated, "GetTurns("), call => call.Contains("Handle)"));
    }

    /// <summary>
    /// The mark travels onto every projection of the value, because the shape it came from is not reachable
    /// from the archetype it was projected onto and the analyzer has only the member to go on.
    /// </summary>
    [Fact]
    public void Every_projection_of_it_is_marked_for_the_analyzer()
    {
        var generated = Generated(Run(Shapes));

        Assert.Contains($"{Mark}\n    public bool CurseActive", generated);
        Assert.DoesNotContain($"{Mark}\n    public int CurseTurns", generated);
    }

    /// <summary>
    /// A client is normally kept away from a replicated setter and writes through a mapping instead. The one
    /// it may write is this one, so the setter has to still be there for the guess to go through.
    /// </summary>
    [Fact]
    public void A_client_keeps_the_setter_it_is_otherwise_kept_from()
    {
        var client = Generated(Run(Shapes, runtime: "Client"));

        Assert.Contains("public bool CurseActive", client);
        Assert.Contains("set => ", client);
        Assert.DoesNotContain("public int CurseTurns\n    {", client);
    }

    // -- where a guess cannot work -------------------------------------------------------------------

    /// <summary>Nothing replicates it, so a guess at it would have nothing to fall through to when dropped.</summary>
    [Fact]
    public void A_value_nobody_replicates_cannot_be_guessed_at()
        => AssertError("READYM050",
            """
            [ArchetypeMixin(Replication.None)]
            public readonly partial struct Mood
            {
                [Predictable]
                public partial int Cheer { get; set; }
            }
            """);

    /// <summary>The guess moves no index, so a lookup would disagree with a read of the same entity.</summary>
    [Fact]
    public void The_value_a_shape_is_found_by_cannot_be_guessed_at()
        => AssertError("READYM051",
            """
            [ArchetypeMixin(Replication.ServerAuthoritative)]
            public readonly partial struct Ticketed
            {
                [Index]
                [Predictable]
                public partial int Ticket { get; set; }
            }
            """);

    [Fact]
    public void A_value_that_cannot_be_written_cannot_be_guessed_at()
        => AssertError("READYM052",
            """
            [ArchetypeMixin(Replication.ServerAuthoritative)]
            public readonly partial struct Fated
            {
                [Predictable]
                public partial int Doom { get; }
            }
            """);

    /// <summary>A collection changes through its own members, so there is no one write to hold as a guess.</summary>
    [Fact]
    public void A_collection_cannot_be_guessed_at()
        => AssertError("READYM053",
            """
            [ArchetypeMixin(Replication.ServerAuthoritative)]
            public readonly partial struct Roster
            {
                [Predictable]
                private partial global::Yooni.Native.Container.NativeList<int> Names { get; set; }
            }
            """);

    /// <summary>The ordinary case still has to get through all four of them.</summary>
    [Fact]
    public void A_guessable_value_is_reported_for_nothing()
        => Assert.Empty(Reported(Run(Shapes)).Where(d => d.Severity == DiagnosticSeverity.Error));

    private void AssertError(string id, string shapes)
    {
        var error = Assert.Single(Reported(Run(shapes)), d => d.Id == id);

        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
    }

    /// <summary>The lines that call a member, with the whitespace the generator indented them by taken off.</summary>
    private static string[] Calls(string generated, string of)
        => [.. generated.Split('\n').Select(line => line.Trim()).Where(line => line.Contains(of))];

    private static string Generated(SourceGeneratorTestHelper.GeneratorRunResult result)
        => string.Concat(result.GeneratedSyntaxTrees.Select(tree => tree.ToString())).Replace("\r\n", "\n");

    private static Diagnostic[] Reported(SourceGeneratorTestHelper.GeneratorRunResult run)
        => [.. run.DriverRunResult.Diagnostics.Where(d => d.Id.StartsWith("READYM"))];

    private SourceGeneratorTestHelper.GeneratorRunResult Run(string shapes, string runtime = "Server")
        => SourceGeneratorTestHelper.RunGenerators(
            [(
                "Shapes.cs",
                $"""
                 using ReadyM.SDK.Attributes;

                 namespace Mod;

                 {shapes}
                 """
            )],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator()],
            output,
            Sdk,
            SourceGeneratorTestHelper.SdkInternalsAssembly,
            buildProperties: new Dictionary<string, string> { ["build_property.SdkRuntime"] = runtime });
}
