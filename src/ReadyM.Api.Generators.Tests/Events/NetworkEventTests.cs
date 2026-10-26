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
    public void The_policy_is_checked_before_any_handler_runs()
    {
        var server = Generated(RunServer());

        Assert.True(
            server.IndexOf("Policy", StringComparison.Ordinal)
            < server.IndexOf("Check(", StringComparison.Ordinal));
    }

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
        var guard = server.IndexOf("cancellation.IsCancelled", first, StringComparison.Ordinal);

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

    // -- routing --------------------------------------------------------------------------------------

    private const string Transport = "global::ReadyM.SDK.Events.NetworkEventTransport";

    private const string ServerTransport = "global::ReadyM.SDK.Server.Events.ServerNetworkEventTransport";

    private const string ServerScopes = "global::ReadyM.SDK.Server.Events.ServerNetworkEventScopes";

    private const string ServerPolicy = "global::ReadyM.SDK.Server.Events.ServerNetworkEventPolicy";

    private const string Audience = "global::ReadyM.SDK.Events.EventAudience";

    private const string AuditHandler = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Server;

        [Service]
        public partial class Auditor
        {
            [EventHandler(Stage.Apply)]
            public void Write(in AuditEvent e) { }
        }
        """;

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

    private const string OpenHandler = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Server;

        [Service]
        public partial class Crier
        {
            [EventHandler(Stage.Apply)]
            public void Heard(in ShoutEvent e) { }
        }
        """;

    private const string WhisperHandler = """
        using ReadyM.SDK.Attributes;
        using ReadyM.SDK.Events;
        using Other.Common;

        namespace Other.Server;

        [Service]
        public partial class Postman
        {
            [EventHandler(Stage.Apply)]
            public void Deliver(in WhisperEvent e) { }
        }
        """;

    /// <summary>
    /// Dispatch derives from its own side's base, which is how the seams reach the world and the relay through a
    /// typed this. A client dispatcher deriving from the server's would only show up as a mod failing to build.
    /// </summary>
    [Fact]
    public void A_dispatcher_derives_from_its_own_sides_base()
    {
        Assert.Contains(") : global::ReadyM.SDK.Server.Events.ServerNetworkEventDispatchBase", Generated(RunServer()));
        Assert.Contains(") : global::ReadyM.SDK.Events.NetworkEventDispatchBase", Generated(RunClient()));
    }

    /// <summary>
    /// A client never sends an event to a peer. It goes to the server, which is what lets the server validate,
    /// change or stop it before anyone else hears about it.
    /// </summary>
    [Fact]
    public void A_client_sends_an_event_only_to_the_server()
    {
        var client = Generated(RunClient());

        Assert.Contains($"{Transport}.SendToServer(", client);
        Assert.DoesNotContain("RelayMode", client);
    }

    /// <summary>
    /// Dispatched on the server and sent nowhere. The first assertion is what makes this a real test: an empty
    /// generator satisfies the second on its own.
    /// </summary>
    [Fact]
    public void A_server_scoped_event_is_dispatched_and_goes_no_further()
    {
        var server = Generated(RunServerWith(AuditHandler));

        Assert.Contains("Write(in ", server);
        Assert.DoesNotContain($"{ServerTransport}.FanOut", server);
    }

    [Fact]
    public void A_cell_scoped_event_goes_to_the_cell_its_subject_is_in()
    {
        var server = Generated(RunServer());

        Assert.Contains($"{ServerScopes}.TryCellOf(this, e.Attacker, out var audience)", server);
        Assert.Contains($"{ServerTransport}.FanOut(this, {Audience}.InCell(audience)", server);
    }

    /// <summary>
    /// A subject that is in no cell leaves the event with no audience. One bad event must not take the server
    /// down, so the lookup is try-shaped and dispatch drops it, the way a failed shape lookup skips an RPC
    /// handler.
    /// </summary>
    [Fact]
    public void An_event_whose_subject_has_no_cell_is_dropped_rather_than_thrown()
        => Assert.Contains($"if (!{ServerScopes}.TryCellOf(", Generated(RunServer()));

    [Fact]
    public void A_single_scoped_event_goes_to_the_player_its_target_names()
        => Assert.Contains($"{Audience}.Single(", Generated(RunServerWith(WhisperHandler)));

    // -- the policy gate ------------------------------------------------------------------------------

    /// <summary>
    /// The gate runs on the server before any handler, and reads the subject the declaration named. A client
    /// that raises what its policy forbids is refused here, which is the only place it can be.
    /// </summary>
    [Fact]
    public void The_subject_is_checked_against_the_policy_before_any_handler_runs()
        => Assert.Contains(
            $"{ServerPolicy}.Allows(this, global::ReadyM.SDK.Attributes.Policy.SubjectOwner, e.Attacker, origin)",
            Generated(RunServer()));

    /// <summary>
    /// By name, not by ordinal. A generator cannot reference the SDK, so it mirrors Policy; emitting the number
    /// would silently re-point every declaration the moment a member is inserted into the real enum, which is
    /// what inserting Anyone at the top of it would have done.
    /// </summary>
    [Fact]
    public void The_policy_is_emitted_by_name_so_reordering_the_enum_cannot_change_it()
    {
        var server = Generated(RunServer());

        Assert.DoesNotContain("(global::ReadyM.SDK.Attributes.Policy)", server);
    }

    /// <summary>An event anyone may raise still passes through the gate, which lets everyone through.</summary>
    [Fact]
    public void An_event_anyone_may_raise_is_still_checked_against_the_policy()
        => Assert.Contains(
            "global::ReadyM.SDK.Attributes.Policy.Anyone",
            Generated(RunServerWith(OpenHandler, OpenEvent)));

    /// <summary>A refusal is a reply, so it goes to the origin and never to the audience.</summary>
    [Fact]
    public void A_rejection_goes_to_the_origin_alone()
    {
        var server = Generated(RunServer());

        Assert.Contains($"{ServerTransport}.Reject(this, ", server);
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
        => SourceGeneratorTestHelper.RunGenerators(
            [("HitView.cs", handlers)], [new NetworkEventHandlerGenerator()], output, Client,
            alsoReferenceMetadata: [RunCommon().OutputCompilation.ToMetadataReference()],
            withoutReferences: AsAClientMod);
}
