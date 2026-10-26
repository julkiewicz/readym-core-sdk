using System.Reflection;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Archetypes;
using Xunit;

namespace ReadyM.Api.Generators.Tests.Events;

/// <summary>
/// A network event is declared in a mod's Common project and handled on either side by staged handlers. The
/// declaration decides who receives it and who may raise it; the handlers decide what happens, in an order the
/// stage names rather than a priority number. These describe what the generators owe that design, and all of
/// them fail until the generators exist.
/// </summary>
public class NetworkEventTests(ITestOutputHelper output)
{
    private static readonly Assembly[] Common =
    [
        typeof(Api.Multiplayer.ClientToClientsAttribute).Assembly,
        typeof(SDK.Attributes.NetworkEventAttribute).Assembly
    ];

    private static readonly Assembly[] Server = [.. Common, typeof(SDK.Server.Entities.IEntities).Assembly];

    private static readonly Assembly[] Client = [.. Common, typeof(SDK.Client.Entities.IEntities).Assembly];

    /// <summary>A client mod cannot see the server SDK, which is how the generator tells the sides apart.</summary>
    private static readonly string[] AsAClientMod =
    [
        typeof(Relay.Server.Sdk.Rpc.ServerRpcHandlersBase).Assembly.GetName().Name!,
    ];

    private const string Marker = "global::ReadyM.SDK.Events.INetworkEvent";

    private const string Codec = "global::ReadyM.SDK.Entities.RpcEntityCodec";

    private const string Shapes = """
        using ReadyM.SDK.Attributes;

        namespace Other.Common;

        [Archetype]
        public readonly partial struct Hero { }

        [Archetype(Networked = false)]
        public readonly partial struct Prop { }
        """;

    private const string Events = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Archetypes.Core;

        namespace Other.Common;

        public enum HitReject { OutOfRange, Invulnerable }

        [NetworkEvent(Scope.Cell, Policy.SubjectOwner, Reject = typeof(HitReject))]
        public partial struct HitEvent
        {
            [Subject] public Hero Attacker { get; set; }
            public Hero? Victim { get; set; }
            public int Damage { get; set; }
        }

        [NetworkEvent(Scope.Server, Policy.ServerAuthoritative)]
        public partial struct AuditEvent
        {
            [Subject] public Hero Who { get; set; }
        }

        [NetworkEvent(Scope.Single, Policy.ServerAuthoritative)]
        public partial struct WhisperEvent
        {
            [Subject] public Hero From { get; set; }
            [Target] public Player To { get; set; }
            public int Line { get; set; }
        }
        """;

    private const string ServerHandlers = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Server;

        [Service]
        public partial class Referee
        {
            [EventHandler(Stage.Validate)]
            public void Check(in HitEvent e, EventContext ctx) { }

            [EventHandler(Stage.Filter)]
            public void Soften(ref HitEvent e, EventContext ctx) { }

            [EventHandler(Stage.Apply)]
            public void Land(in HitEvent e) { }

            [EventHandler(Stage.Observe)]
            public void Count(in HitEvent e, EventContext ctx) { }

            [EventHandler(Stage.Rejected)]
            public void Refused(in HitEvent e, HitReject reason, EventContext ctx) { }
        }
        """;

    private const string ClientHandlers = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Client;

        [Service]
        public partial class HitView
        {
            [EventHandler(Stage.Apply, SkipOnRaiser = true)]
            public void Play(in HitEvent e) { }

            [EventHandler(Stage.Observe, After = "Other.Scoreboard")]
            public void Log(in HitEvent e, EventContext ctx) { }

            [EventHandler(Stage.Rejected)]
            public void Fizzle(in HitEvent e, HitReject reason, EventContext ctx) { }
        }
        """;

    // -- the declaration ------------------------------------------------------------------------------

    [Fact]
    public void An_event_declaration_compiles() => AssertCompiles(RunCommon());

    [Fact]
    public void Every_event_struct_gains_the_marker()
    {
        var common = Generated(RunCommon());

        Assert.Contains($"partial struct HitEvent : {Marker}", common);
        Assert.Contains($"partial struct AuditEvent : {Marker}", common);
        Assert.Contains($"partial struct WhisperEvent : {Marker}", common);
    }

    /// <summary>Both ends assign the same id to the same event, or they misread each other's stream.</summary>
    [Fact]
    public void Every_event_gets_an_id_from_a_manifest_named_for_its_assembly()
    {
        var common = Generated(RunCommon());

        Assert.Contains("class NetworkEventManifest", common);
        Assert.Contains("public const string Id = \"Other.Common\";", common);
        Assert.Contains("AuditEventCode", common);
        Assert.Contains("HitEventCode", common);
        Assert.Contains("WhisperEventCode", common);
    }

    /// <summary>Ordinal by name, so the codes do not depend on the order the declarations are read in.</summary>
    [Fact]
    public void The_ids_are_assigned_in_name_order()
    {
        var common = Generated(RunCommon());

        Assert.True(
            common.IndexOf("AuditEventCode", StringComparison.Ordinal)
            < common.IndexOf("HitEventCode", StringComparison.Ordinal));
        Assert.True(
            common.IndexOf("HitEventCode", StringComparison.Ordinal)
            < common.IndexOf("WhisperEventCode", StringComparison.Ordinal));
    }

    [Fact]
    public void A_shape_member_travels_as_its_entity_and_a_nullable_one_may_be_absent()
    {
        var common = Generated(RunCommon());

        Assert.Contains($"{Codec}.Write(writer, Attacker, \"Attacker\");", common);
        Assert.Contains($"{Codec}.WriteOptional(writer, Victim, \"Victim\");", common);
    }

    [Fact]
    public void Plain_members_serialize_in_declaration_order()
    {
        var common = Generated(RunCommon());

        Assert.True(
            common.IndexOf("\"Victim\"", StringComparison.Ordinal)
            < common.IndexOf("Damage", StringComparison.Ordinal));
    }

    // -- what a declaration may not say ---------------------------------------------------------------

    [Fact]
    public void A_single_scoped_event_without_a_target_is_an_error()
        => AssertError("NEVT001", """
            using ReadyM.SDK.Attributes;
            namespace Other.Common;
            [Archetype] public readonly partial struct Hero { }
            [NetworkEvent(Scope.Single, Policy.ServerAuthoritative)]
            public partial struct Lonely { [Subject] public Hero Who { get; set; } }
            """);

    [Fact]
    public void A_target_on_an_event_that_is_not_single_scoped_is_an_error()
        => AssertError("NEVT002", """
            using ReadyM.SDK.Attributes;
            using ReadyM.SDK.Archetypes.Core;
            namespace Other.Common;
            [Archetype] public readonly partial struct Hero { }
            [NetworkEvent(Scope.Cell, Policy.SubjectOwner)]
            public partial struct Misaimed
            {
                [Subject] public Hero Who { get; set; }
                [Target] public Player To { get; set; }
            }
            """);

    [Fact]
    public void An_area_scoped_event_with_no_subject_and_no_area_member_is_an_error()
        => AssertError("NEVT003", """
            using ReadyM.SDK.Attributes;
            namespace Other.Common;
            [NetworkEvent(Scope.Area, Policy.ServerAuthoritative)]
            public partial struct Nowhere { public int What { get; set; } }
            """);

    [Fact]
    public void A_policy_that_checks_the_subject_needs_one()
        => AssertError("NEVT004", """
            using ReadyM.SDK.Attributes;
            namespace Other.Common;
            [NetworkEvent(Scope.Global, Policy.SubjectOwner)]
            public partial struct Subjectless { public int What { get; set; } }
            """);

    /// <summary>The counterpart to NEVT004: a policy that checks nothing needs nothing to check.</summary>
    [Fact]
    public void A_policy_that_checks_nobody_needs_no_subject()
        => AssertNoError("""
            using ReadyM.SDK.Attributes;
            namespace Other.Common;
            [NetworkEvent(Scope.Global, Policy.Anyone)]
            public partial struct Shout { public int What { get; set; } }
            """);

    [Fact]
    public void Two_subjects_are_an_error()
        => AssertError("NEVT005", """
            using ReadyM.SDK.Attributes;
            namespace Other.Common;
            [Archetype] public readonly partial struct Hero { }
            [NetworkEvent(Scope.Cell, Policy.SubjectOwner)]
            public partial struct Twofold
            {
                [Subject] public Hero One { get; set; }
                [Subject] public Hero Two { get; set; }
            }
            """);

    [Fact]
    public void A_member_whose_shape_is_never_networked_is_an_error()
        => AssertError("NEVT006", """
            using ReadyM.SDK.Attributes;
            namespace Other.Common;
            [Archetype] public readonly partial struct Hero { }
            [Archetype(Networked = false)] public readonly partial struct Prop { }
            [NetworkEvent(Scope.Cell, Policy.SubjectOwner)]
            public partial struct Unsendable
            {
                [Subject] public Hero Who { get; set; }
                public Prop Thing { get; set; }
            }
            """);

    // -- the ladder -----------------------------------------------------------------------------------

    [Fact]
    public void A_server_with_handlers_compiles() => AssertCompiles(RunServer());

    [Fact]
    public void A_client_with_handlers_compiles() => AssertCompiles(RunClient());

    /// <summary>
    /// Dispatch takes its handlers as constructor parameters, so a public dispatch over a handler the mod keeps
    /// to itself does not compile. Mods write internal handler classes as a matter of course.
    /// </summary>
    [Fact]
    public void A_handler_the_mod_keeps_to_itself_compiles()
        => AssertCompiles(RunServerWith(InternalHandler));

    [Fact]
    public void The_stages_run_in_their_declared_order()
    {
        var server = Generated(RunServer());

        var validate = server.IndexOf("Check(", StringComparison.Ordinal);
        var filter = server.IndexOf("Soften(", StringComparison.Ordinal);
        var apply = server.IndexOf("Land(", StringComparison.Ordinal);
        var observe = server.IndexOf("Count(", StringComparison.Ordinal);

        Assert.True(validate < filter, "Validate runs before Filter");
        Assert.True(filter < apply, "Filter runs before Apply");
        Assert.True(apply < observe, "Apply runs before Observe");
    }

    private const string TwoValidators = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Server;

        [Service]
        public partial class Rules
        {
            [EventHandler(Stage.Validate)]
            public void CheckRange(in HitEvent e, EventContext ctx) { }

            [EventHandler(Stage.Validate)]
            public void CheckArmour(in HitEvent e, EventContext ctx) { }
        }
        """;

    /// <summary>
    /// A refusal stops the rest of its own stage, not just the stages after it, so the guard belongs between the
    /// handlers rather than only at the end of the stage.
    /// </summary>
    [Fact]
    public void A_refusal_stops_the_rest_of_its_own_stage()
    {
        var server = Generated(RunServerWith(TwoValidators));

        var first = server.IndexOf("CheckRange(", StringComparison.Ordinal);
        var second = server.IndexOf("CheckArmour(", StringComparison.Ordinal);
        var guard = server.IndexOf("ctx.Cancellation.IsCancelled", first, StringComparison.Ordinal);

        Assert.True(first >= 0 && second > first, "both validators are dispatched");
        Assert.True(guard > first && guard < second, "the refusal is checked before the second validator runs");
    }

    /// <summary>A filter is the only stage that may rewrite the event, so it is the only one taking it by ref.</summary>
    [Fact]
    public void Only_a_filter_is_handed_the_event_by_reference()
    {
        var server = Generated(RunServer());

        Assert.Contains("Soften(ref ", server);
        Assert.Contains("Check(in ", server);
        Assert.Contains("Land(in ", server);
    }

    [Fact]
    public void A_handler_that_takes_the_event_by_ref_outside_filter_is_an_error()
        => AssertServerError("NEVT010", """
            using ReadyM.SDK.Attributes;
            using ReadyM.SDK.Events;
            using Other.Common;
            namespace Other.Server;
            [Service]
            public partial class Rogue
            {
                [EventHandler(Stage.Observe)]
                public void Rewrite(ref HitEvent e, EventContext ctx) { }
            }
            """);

    /// <summary>
    /// A mod writes the handler, so the generator emits the call: the reason is cast to the declared type on the
    /// way in, which is what makes a switch over it exhaustive.
    /// </summary>
    [Fact]
    public void A_rejected_handler_is_handed_the_reject_type_the_event_declared()
    {
        var server = Generated(RunServer());

        Assert.Contains("Refused(in e, ", server);
        Assert.Contains("global::Other.Common.HitReject", server);
    }

    [Fact]
    public void A_rejected_handler_whose_reason_is_the_wrong_type_is_an_error()
        => AssertServerError("NEVT011", """
            using ReadyM.SDK.Attributes;
            using ReadyM.SDK.Events;
            using Other.Common;
            namespace Other.Server;
            [Service]
            public partial class Rogue
            {
                [EventHandler(Stage.Rejected)]
                public void Refused(in HitEvent e, int reason, EventContext ctx) { }
            }
            """);

    [Fact]
    public void A_rejected_handler_for_an_event_that_declared_no_reject_type_is_an_error()
        => AssertServerError("NEVT012", """
            using ReadyM.SDK.Attributes;
            using ReadyM.SDK.Events;
            using Other.Common;
            namespace Other.Server;
            [Service]
            public partial class Rogue
            {
                [EventHandler(Stage.Rejected)]
                public void Refused(in AuditEvent e, int reason, EventContext ctx) { }
            }
            """);

    /// <summary>The raiser is the machine that called Raise, which the context knows, not a comparison of ids.</summary>
    [Fact]
    public void An_apply_may_be_skipped_on_the_machine_that_raised_it()
    {
        var client = Generated(RunClient());

        Assert.Contains("if (!ctx.RaisedHere)", client);
        Assert.Contains("Play(in ", client);
    }

    [Fact]
    public void Skipping_the_raiser_outside_apply_is_an_error()
        => AssertServerError("NEVT013", """
            using ReadyM.SDK.Attributes;
            using ReadyM.SDK.Events;
            using Other.Common;
            namespace Other.Server;
            [Service]
            public partial class Rogue
            {
                [EventHandler(Stage.Observe, SkipOnRaiser = true)]
                public void Count(in HitEvent e, EventContext ctx) { }
            }
            """);

    [Fact]
    public void An_after_constraint_orders_one_mod_behind_another()
        => Assert.Contains("Other.Scoreboard", Generated(RunClient()));

    // -- which machine runs what ----------------------------------------------------------------------

    [Fact]
    public void A_validate_handler_in_a_client_project_is_an_error()
        => AssertClientError("NEVT020", """
            using ReadyM.SDK.Attributes;
            using ReadyM.SDK.Events;
            using Other.Common;
            namespace Other.Client;
            [Service]
            public partial class Eager
            {
                [EventHandler(Stage.Validate)]
                public void Check(in HitEvent e, EventContext ctx) { }
            }
            """);

    [Fact]
    public void A_filter_handler_in_a_client_project_is_an_error()
        => AssertClientError("NEVT020", """
            using ReadyM.SDK.Attributes;
            using ReadyM.SDK.Events;
            using Other.Common;
            namespace Other.Client;
            [Service]
            public partial class Eager
            {
                [EventHandler(Stage.Filter)]
                public void Soften(ref HitEvent e, EventContext ctx) { }
            }
            """);

    // -- what the contract says about the event --------------------------------------------------------

    private const string Contract = "global::ReadyM.SDK.Events.INetworkEventContract";

    private const string Handlers = "global::ReadyM.SDK.Events.INetworkEventHandlers";

    private const string Attributes = "global::ReadyM.SDK.Attributes";

    private const string Handle = "global::ReadyM.SDK.Entities.EntityHandle";

    /// <summary>Nothing outside a mod calls its handlers, so a mod is free to keep the class to itself.</summary>
    private const string InternalHandler = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Server;

        [Service]
        internal sealed partial class Bookkeeper
        {
            [EventHandler(Stage.Apply)]
            public void Note(in HitEvent e) { }
        }
        """;

    /// <summary>Kept apart from the shared events so adding it cannot shift what the other tests read.</summary>
    private const string OpenEvent = """
        using ReadyM.SDK.Attributes;

        namespace Other.Common;

        [NetworkEvent(Scope.Global, Policy.Anyone)]
        public partial struct ShoutEvent
        {
            public int Line { get; set; }
        }
        """;

    private const string OpenClientHandler = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Client;

        [Service]
        public partial class Listener
        {
            [EventHandler(Stage.Observe)]
            public void Heard(in ShoutEvent e) { }

        }
        """;

    /// <summary>
    /// One contract per event, in the project that declares it, rather than one per mod that handles it.
    /// That is what lets the coordinator ask the policy once and send the event out once however many mods
    /// are listening; a contract per mod would be back to a fan-out per mod.
    /// </summary>
    [Fact]
    public void Each_declared_event_gets_one_contract()
    {
        var common = Generated(RunCommon());

        Assert.Contains($"public sealed class HitEventContract : {Contract}<global::Other.Common.HitEvent>", common);
        Assert.Contains($"public sealed class AuditEventContract : {Contract}<global::Other.Common.AuditEvent>", common);
        Assert.Contains($"public sealed class WhisperEventContract : {Contract}<global::Other.Common.WhisperEvent>", common);
    }

    /// <summary>
    /// The code comes from the manifest the mod's Common project generated, shifted by the block the host
    /// gave that mod, so two mods loaded together never collide.
    /// </summary>
    [Fact]
    public void The_contract_carries_the_events_code()
        => Assert.Contains(
            "        => NetworkEventManifest.HitEventCode + NetworkEventManifest.Offset;",
            Generated(RunCommon()));

    [Fact]
    public void The_contract_carries_the_scope_the_event_was_declared_with()
    {
        var common = Generated(RunCommon());

        Assert.Contains($"public {Attributes}.Scope Scope => {Attributes}.Scope.Cell;", common);
        Assert.Contains($"public {Attributes}.Scope Scope => {Attributes}.Scope.Server;", common);
        Assert.Contains($"public {Attributes}.Scope Scope => {Attributes}.Scope.Single;", common);
    }

    /// <summary>
    /// The subject the policy is checked against and a scoped audience is read from, as a handle, so the
    /// server's checks need nothing generated per shape.
    /// </summary>
    [Fact]
    public void The_contract_carries_the_subject_as_a_handle()
        => Assert.Contains($"=> {Handle}.Of(e.Attacker);", Generated(RunCommon()));

    [Fact]
    public void A_single_scoped_event_names_the_player_it_goes_to()
        => Assert.Contains("=> e.To.PlayerId;", Generated(RunCommon()));

    /// <summary>An event that goes to an audience has no one player, and says so rather than guessing one.</summary>
    [Fact]
    public void An_event_that_is_not_single_scoped_names_no_player()
        => Assert.Contains("=> global::ReadyM.Api.Idents.PlayerId.Invalid;", Generated(RunCommon()));

    /// <summary>
    /// By name, not by ordinal. A generator cannot reference the SDK, so it mirrors Policy; emitting the
    /// number would silently re-point every declaration the moment a member is inserted into the real enum,
    /// which is what inserting Anyone at the top of it would have done.
    /// </summary>
    [Fact]
    public void The_policy_is_emitted_by_name_so_reordering_the_enum_cannot_change_it()
    {
        var common = Generated(RunCommon());

        Assert.Contains($"public {Attributes}.Policy Policy => {Attributes}.Policy.SubjectOwner;", common);
        Assert.DoesNotContain($"({Attributes}.Policy)", common);
    }

    [Fact]
    public void An_event_anyone_may_raise_says_so_in_its_contract()
        => Assert.Contains(
            $"public {Attributes}.Policy Policy => {Attributes}.Policy.Anyone;",
            Generated(RunCommon(OpenEvent)));

    // -- what goes on the wire ------------------------------------------------------------------------

    /// <summary>
    /// A refusal is not the event: the origin is owed the reason and the mod that gave it. The reason gets a
    /// presence byte because an event may name a reject type and still be refused without one, by the policy
    /// or by a bare Cancel().
    /// </summary>
    [Fact]
    public void A_refusal_carries_its_reason_and_reads_back_without_one()
    {
        var common = Generated(RunCommon());

        Assert.Contains("if (reason is global::Other.Common.HitReject given)", common);
        Assert.Contains("writer.Put((byte)1);", common);
        Assert.Contains("writer.Put((byte)0);", common);
        Assert.Contains("=> reader.GetByte() == 1 ? (global::Other.Common.HitReject)reader.GetInt()", common);
    }

    /// <summary>An event that named no reject type is refused bare, so there is nothing to write or read.</summary>
    [Fact]
    public void An_event_with_no_reject_type_carries_no_reason()
        => Assert.Contains("public object? ReadReason(NetDataReader reader) => null;", Generated(RunCommon()));

    /// <summary>
    /// Reading needs the world and writing does not, so the event serializes itself and the contract reads
    /// it back. One reader per event rather than one per mod: two mods reading the same bytes differently is
    /// not a failure anything would report.
    /// </summary>
    [Fact]
    public void The_contract_reads_the_event_back_off_the_wire()
    {
        var common = Generated(RunCommon());

        Assert.Contains("public bool TryRead(global::ReadyM.SDK.Events.NetworkEventDispatchCore world,", common);
        Assert.Contains("world, reader, out var m0, \"HitEvent\", \"Attacker\"", common);
    }

    // -- what a mod generates -------------------------------------------------------------------------

    /// <summary>
    /// A mod emits handlers and nothing else. It used to emit a dispatcher of its own, which subscribed,
    /// checked the policy and fanned out, so two mods on one event did all three twice.
    /// </summary>
    [Fact]
    public void A_mod_emits_a_handler_set_and_no_dispatcher_of_its_own()
    {
        foreach (var generated in new[] { Generated(RunServer()), Generated(RunClient()) })
        {
            Assert.Contains($") : {Handlers}<global::Other.Common.HitEvent>", generated);
            Assert.DoesNotContain("NetworkEventDispatchBase", generated);
            Assert.DoesNotContain("FanOut", generated);
            Assert.DoesNotContain("ServerNetworkEventPolicy", generated);
        }
    }

    /// <summary>
    /// One stage at a time, so the coordinator can finish a stage across every mod before the next starts.
    /// A case per stage is what makes another mod's Validate able to refuse what this one would apply.
    /// </summary>
    [Fact]
    public void A_handler_set_runs_one_stage_at_a_time()
    {
        var server = Generated(RunServer());

        Assert.Contains($"public void Run({Attributes}.Stage stage, ref global::Other.Common.HitEvent e,", server);
        Assert.Contains($"case {Attributes}.Stage.Validate:", server);
        Assert.Contains($"case {Attributes}.Stage.Rejected:", server);
    }

    /// <summary>The two stages that always run on the server are not emitted where they could never fire.</summary>
    [Fact]
    public void A_client_handler_set_has_no_server_only_stages()
    {
        var client = Generated(RunClient());

        Assert.Contains($"case {Attributes}.Stage.Apply:", client);
        Assert.DoesNotContain($"case {Attributes}.Stage.Validate:", client);
        Assert.DoesNotContain($"case {Attributes}.Stage.Filter:", client);
    }

    /// <summary>A mod with nothing to run on a refusal emits no case for one.</summary>
    [Fact]
    public void A_mod_with_no_rejected_handler_emits_no_rejected_case()
        => Assert.DoesNotContain(
            $"case {Attributes}.Stage.Rejected:", Generated(RunClientWith(OpenClientHandler, OpenEvent)));

    /// <summary>
    /// A refusal may carry no reason, by the policy or by a bare Cancel(), and a handler that asked for one
    /// has nothing to be handed. Casting null to the declared enum would throw rather than refuse.
    /// </summary>
    [Fact]
    public void A_rejected_handler_runs_only_where_there_is_a_reason_to_hand_it()
        => Assert.Contains(
            "if (ctx.Cancellation.Reason is global::Other.Common.HitReject reason0)", Generated(RunServer()));

    /// <summary>
    /// Both run themselves where the target has a module initializer, and both are listed per assembly so a
    /// netstandard2.0 target, which has none, can be run by the mod loader instead.
    /// </summary>
    [Fact]
    public void An_assembly_lists_what_it_declared_so_a_host_can_run_it()
    {
        Assert.Contains(
            "global::ReadyM.SDK.Events.NetworkEventRegistry.Declare<global::Other.Common.HitEvent>("
            + "new global::Other.Common.HitEventContract());",
            Generated(RunCommon()));

        Assert.Contains("public static void RegisterAll()", Generated(RunServer()));
        Assert.Contains("global::ReadyM.SDK.Events.NetworkEventRegistry.Handle<", Generated(RunServer()));
    }

    [Fact]
    public void A_handler_set_says_which_side_it_belongs_to()
    {
        Assert.Contains("global::ReadyM.Api.Multiplayer.RPC.RpcSide.Server", Generated(RunServer()));
        Assert.Contains("global::ReadyM.Api.Multiplayer.RPC.RpcSide.Client", Generated(RunClient()));
    }

    // -- helpers --------------------------------------------------------------------------------------

    private static void AssertCompiles(SourceGeneratorTestHelper.GeneratorRunResult result)
        => Assert.Empty(result.OutputDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

    private static string Generated(SourceGeneratorTestHelper.GeneratorRunResult result)
        => string.Concat(result.GeneratedSyntaxTrees.Select(tree => tree.ToString())).Replace("\r\n", "\n");

    private void AssertError(string id, string events)
    {
        var diagnostics = RunCommon(events).CompilationDiagnostics;

        Assert.Contains(diagnostics, d => d.Id == id && d.Severity == DiagnosticSeverity.Error);
    }

    private void AssertNoError(string events)
        => Assert.Empty(RunCommon(events).CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error));

    private void AssertServerError(string id, string handlers)
    {
        var diagnostics = RunServer(handlers).CompilationDiagnostics;

        Assert.Contains(diagnostics, d => d.Id == id && d.Severity == DiagnosticSeverity.Error);
    }

    private void AssertClientError(string id, string handlers)
    {
        var diagnostics = RunClient(handlers).CompilationDiagnostics;

        Assert.Contains(diagnostics, d => d.Id == id && d.Severity == DiagnosticSeverity.Error);
    }

    /// <summary>The mod's Common project, where the events and the shapes they name live together.</summary>
    private SourceGeneratorTestHelper.GeneratorRunResult RunCommon(string events = Events)
        => SourceGeneratorTestHelper.RunGenerators(
            [("Shapes.cs", Shapes), ("Events.cs", events)],
            [new ArchetypeGenerator(), new ArchetypeMixinGenerator(), new NetworkEventContractGenerator()],
            output, Common,
            assemblyName: "Other.Common");

    private SourceGeneratorTestHelper.GeneratorRunResult RunServer(string handlers = ServerHandlers)
        => RunServerWith(handlers);

    private SourceGeneratorTestHelper.GeneratorRunResult RunServerWith(string handlers, string events = Events)
        => SourceGeneratorTestHelper.RunGenerators(
            [("Referee.cs", handlers)], [new NetworkEventHandlerGenerator()], output, Server,
            alsoReferenceMetadata: [RunCommon(events).OutputCompilation.ToMetadataReference()]);

    private SourceGeneratorTestHelper.GeneratorRunResult RunClient(string handlers = ClientHandlers)
        => RunClientWith(handlers);

    private SourceGeneratorTestHelper.GeneratorRunResult RunClientWith(string handlers, string events = Events)
        => SourceGeneratorTestHelper.RunGenerators(
            [("HitView.cs", handlers)], [new NetworkEventHandlerGenerator()], output, Client,
            alsoReferenceMetadata: [RunCommon(events).OutputCompilation.ToMetadataReference()],
            withoutReferences: AsAClientMod);
}
