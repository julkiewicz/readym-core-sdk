using System.Globalization;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;

namespace ReadyM.Api.Generators.Tests;

public sealed class DeriveIGameEventGeneratorTests(ITestOutputHelper output)
{
    // Fakes of the real contexts, with the same names, so each test sets who owns the subject and who is master.
    private const string ContextStubs = """
#pragma warning disable CS0436
namespace ReadyM.Api.Multiplayer.GameEvents
{
    public readonly struct OwnershipContext(bool owns)
    {
        public bool OwnsEntity(global::Friflo.Engine.ECS.Entity entity) => owns;
        public bool OwnsEntity(global::Friflo.Engine.ECS.RawEntity entity) => owns;
    }
}

namespace ReadyM.Relay.Client.GameEvents
{
    public readonly struct MasterClientContext(bool master)
    {
        public bool IsMasterClient => master;
    }
}
""";

    private const string Events = """
using Friflo.Engine.ECS;
using ReadyM.Api.Mapping.Events;

namespace GameEventTests;

[DeriveIGameEvent, OwnershipBased(nameof(Attacker))]
public partial struct OwnershipRaw { public RawEntity Attacker; public int Damage; }

[DeriveIGameEvent, OwnershipBased(nameof(Subject))]
public readonly partial struct OwnershipEntity(Entity subject) { public readonly Entity Subject = subject; }

[DeriveIGameEvent, AlwaysPropagates]
public partial struct Always { public int Value; }

[DeriveIGameEvent, AlwaysPropagatesToEcsOnly]
public partial struct ToEcsOnly { public int Value; }

[DeriveIGameEvent, AlwaysPropagatesToGameOnly]
public partial struct ToGameOnly { public int Value; }

[DeriveIGameEvent, MasterClientManaged]
public readonly partial struct Master { public readonly int Value; }

[DeriveIGameEvent, RunOnMasterClientOnly(nameof(Owner))]
public partial struct RunOnMaster { public RawEntity Owner; }

public static class Probe
{
    private sealed class Registration(bool owns, bool master) : IGameEventContextRegistration
    {
        public void Register(GameEventContextRegistry registry)
        {
            registry.Register(new ReadyM.Api.Multiplayer.GameEvents.OwnershipContext(owns));
            registry.Register(new ReadyM.Relay.Client.GameEvents.MasterClientContext(master));
        }
    }

    public static string Ask<TEvent>(TEvent ev, bool owns, bool master) where TEvent : struct, IGameEvent
    {
        var contexts = new GameEventContextRegistry([new Registration(owns, master)]);
        return $"{ev.CanGameEventNotifyEcs(contexts)} {ev.CanGameEventRunLocally(contexts)} {ev.CanEcsInvokeGameEvent(contexts)}";
    }

    public static string Ask(string name, bool owns, bool master) => name switch
    {
        nameof(OwnershipRaw) => Ask(new OwnershipRaw(), owns, master),
        nameof(OwnershipEntity) => Ask(new OwnershipEntity(default), owns, master),
        nameof(Always) => Ask(new Always(), owns, master),
        nameof(ToEcsOnly) => Ask(new ToEcsOnly(), owns, master),
        nameof(ToGameOnly) => Ask(new ToGameOnly(), owns, master),
        nameof(Master) => Ask(new Master(), owns, master),
        nameof(RunOnMaster) => Ask(new RunOnMaster(), owns, master),
        _ => throw new System.ArgumentOutOfRangeException(nameof(name), name, null),
    };
}
""";

    private Func<string, bool, bool, string> CompileProbe()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<DeriveIGameEventGenerator>(
            [("Stubs.cs", ContextStubs), ("Events.cs", Events)], output);

        var errors = result.OutputDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(errors);

        var assembly = SourceGeneratorTestHelper.EmitToAssembly(result.OutputCompilation, output);
        var ask = assembly.GetType("GameEventTests.Probe")!
            .GetMethod("Ask", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(bool), typeof(bool)])!;
        return (name, owns, master) => (string)ask.Invoke(null, [name, owns, master])!;
    }

    [Fact]
    public void EveryDiscriminatorAnswersTheTable()
    {
        var ask = CompileProbe();
        var cases = new (string Event, bool Owns, bool Master, string Expected)[]
        {
            ("OwnershipRaw", true, false, "Notify RunAll DontRun"),
            ("OwnershipRaw", false, false, "DontNotify Rejected RunAll"),
            ("OwnershipRaw", true, true, "Notify RunAll DontRun"),
            ("OwnershipRaw", false, true, "DontNotify Rejected RunAll"),
            ("OwnershipEntity", true, false, "Notify RunAll DontRun"),
            ("OwnershipEntity", false, false, "DontNotify Rejected RunAll"),

            ("Always", false, false, "Notify RunAll RunAll"),
            ("Always", true, true, "Notify RunAll RunAll"),
            ("ToEcsOnly", false, false, "Notify RunAll DontRun"),
            ("ToEcsOnly", true, true, "Notify RunAll DontRun"),
            ("ToGameOnly", false, false, "DontNotify RunAll RunAll"),
            ("ToGameOnly", true, true, "DontNotify RunAll RunAll"),

            ("Master", false, true, "Notify RunAll DontRun"),
            ("Master", true, true, "Notify RunAll DontRun"),
            ("Master", false, false, "DontNotify Rejected RunAll"),
            ("Master", true, false, "DontNotify Rejected RunAll"),

            ("RunOnMaster", true, true, "DontNotify RunAll RunAll"),
            ("RunOnMaster", false, true, "DontNotify RunAll RunAll"),
            ("RunOnMaster", true, false, "Notify DontRun DontRun"),
            ("RunOnMaster", false, false, "DontNotify Rejected DontRun"),
        };

        foreach (var (name, owns, master, expected) in cases)
        {
            Assert.True(expected == ask(name, owns, master),
                $"{name} owns={owns} master={master}: expected '{expected}', got '{ask(name, owns, master)}'");
        }
    }

    [Fact]
    public void GeneratedPartOfAReadonlyStructIsReadonly()
    {
        var result = SourceGeneratorTestHelper.RunGenerator<DeriveIGameEventGenerator>(
            [("Stubs.cs", ContextStubs), ("Events.cs", Events)], output);

        var master = result.GeneratedSyntaxTrees.Single(t => t.FilePath.Contains("GameEventTests.Master."));
        Assert.Contains("readonly partial struct Master : global::ReadyM.Api.Mapping.Events.IGameEvent", master.ToString());
        var raw = result.GeneratedSyntaxTrees.Single(t => t.FilePath.Contains("GameEventTests.OwnershipRaw."));
        Assert.DoesNotContain("readonly partial struct", raw.ToString());
    }

    private string[] GeneratorErrors(string eventSource)
    {
        var result = SourceGeneratorTestHelper.RunGenerator<DeriveIGameEventGenerator>(
            [("Stubs.cs", ContextStubs), ("Event.cs", eventSource)], output);
        return result.OutputDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage(CultureInfo.InvariantCulture))
            .ToArray();
    }

    [Fact]
    public void SubjectNamingAMissingFieldIsAnError()
    {
        var errors = GeneratorErrors("""
using Friflo.Engine.ECS;
using ReadyM.Api.Mapping.Events;
namespace GameEventTests;
[DeriveIGameEvent, OwnershipBased("Attacker")]
public partial struct Broken { public RawEntity Target; }
""");
        Assert.Contains(errors, e => e.Contains("#error") && e.Contains("Broken") && e.Contains("'Attacker'"));
    }

    [Fact]
    public void SubjectOfAnotherTypeIsAnError()
    {
        var errors = GeneratorErrors("""
using System;
using ReadyM.Api.Mapping.Events;
namespace GameEventTests;
[DeriveIGameEvent, RunOnMasterClientOnly(nameof(Actor))]
public partial struct Broken { public IntPtr Actor; }
""");
        Assert.Contains(errors, e => e.Contains("#error") && e.Contains("Broken") && e.Contains("Entity or RawEntity"));
    }

    [Fact]
    public void TwoDiscriminatorsAreAnError()
    {
        var errors = GeneratorErrors("""
using ReadyM.Api.Mapping.Events;
namespace GameEventTests;
[DeriveIGameEvent, AlwaysPropagates, MasterClientManaged]
public partial struct Broken { public int Value; }
""");
        Assert.Contains(errors, e => e.Contains("#error") && e.Contains("Broken") && e.Contains("one discriminator"));
    }

    [Fact]
    public void ADiscriminatorWithoutDeriveIGameEventIsAnError()
    {
        var errors = GeneratorErrors("""
using ReadyM.Api.Mapping.Events;
namespace GameEventTests;
[AlwaysPropagates]
public partial struct Broken { public int Value; }
""");
        Assert.Contains(errors, e => e.Contains("#error") && e.Contains("Broken") && e.Contains("[DeriveIGameEvent]"));
    }

    [Fact]
    public void DeriveIGameEventWithoutADiscriminatorIsAnError()
    {
        var errors = GeneratorErrors("""
using ReadyM.Api.Mapping.Events;
namespace GameEventTests;
[DeriveIGameEvent]
public partial struct Broken { public int Value; }
""");
        Assert.Contains(errors, e => e.Contains("#error") && e.Contains("Broken") && e.Contains("one discriminator"));
    }

    [Fact]
    public void AnEventWithNeitherADiscriminatorNorItsOwnPolicyIsNotAGameEvent()
    {
        var errors = GeneratorErrors("""
using ReadyM.Api.Mapping.Events;
namespace GameEventTests;
public struct Plain { public int Value; }
public static class Use
{
    public static void Ask<T>(T ev) where T : struct, IGameEvent { }
    public static void Call() => Ask(new Plain());
}
""");
        Assert.Contains(errors, e => e.Contains("Plain") && e.Contains("IGameEvent"));
    }

    [Fact]
    public void AHandWrittenPolicyNeedsNoDiscriminator()
    {
        var errors = GeneratorErrors("""
using ReadyM.Api.Mapping.Events;
namespace GameEventTests;
public readonly struct HandWritten : IGameEvent
{
    public GameEventNotifyResult CanGameEventNotifyEcs(GameEventContextRegistry contexts) => GameEventNotifyResult.Notify;
    public GameEventResult CanGameEventRunLocally(GameEventContextRegistry contexts) => GameEventResult.RunAll;
    public GameEventResult CanEcsInvokeGameEvent(GameEventContextRegistry contexts) => GameEventResult.DontRun;
}
""");
        Assert.Empty(errors);
    }
}
