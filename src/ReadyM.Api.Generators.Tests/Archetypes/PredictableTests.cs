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

        Assert.Contains($"private static readonly {Guesses}<bool> ActiveGuesses", generated);
        Assert.Contains("= new(\"Curse.Active\");", generated);
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
    /// Guessing is a call of its own, never a second meaning for the setter. Whether a write is a guess is
    /// a question about where control came from, and a helper called from inside the callback that guesses
    /// looks exactly like one called from anywhere else.
    /// </summary>
    [Fact]
    public void Guessing_at_it_is_its_own_call()
    {
        var generated = Generated(Run(Shapes));

        Assert.Contains("public static void PredictActive(", generated);
        Assert.Contains("=> ActiveGuesses.Guess(handle, value);", generated);
        Assert.DoesNotContain("PredictTurns", generated);
    }

    /// <summary>The setter keeps meaning the write it always meant, on both sides.</summary>
    [Fact]
    public void The_setter_is_left_alone()
    {
        var generated = Generated(Run(Shapes));

        Assert.DoesNotContain("ActiveGuesses.Guess(handle, value))", generated);
        Assert.Contains(
            "public static void SetActive(in global::ReadyM.SDK.Entities.EntityHandle handle, bool value)",
            generated);
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
    /// Every way of reaching the value can guess at it, under the name the value is reached by there. A
    /// projection that could not be guessed at would send a mod looking for the shape behind it.
    /// </summary>
    [Fact]
    public void Every_projection_of_it_can_be_guessed_at_under_its_own_name()
    {
        var generated = Generated(Run(Shapes));

        Assert.Contains("public void PredictActive(bool value)", generated);
        Assert.Contains("public void PredictCurseActive(bool value)", generated);
    }

    /// <summary>
    /// A client is kept away from a replicated setter and writes through a mapping instead, which the guess
    /// does not change: guessing is the separate call, so the setter needs no exception made for it.
    /// </summary>
    [Fact]
    public void A_client_is_kept_from_the_setter_and_still_gets_the_guess()
    {
        var client = Generated(Run(Shapes, runtime: "Client"));

        Assert.Contains("public bool CurseActive => global::Mod.CurseAccessors.GetActive(_handle);", client);
        Assert.Contains("public void PredictCurseActive(bool value)", client);
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
