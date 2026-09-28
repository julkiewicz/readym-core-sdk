using System.Runtime.InteropServices;
using DryIoc;
using Friflo.Engine.ECS;
using Microsoft.Extensions.Logging;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Helpers;
using ReadyM.Api.Mapping.Events;
using ReadyM.Api.Tests.TestEvents;

namespace ReadyM.Api.Tests;

public class MappedEventManagerTests
{
    private class TestEventsRegistration : INativeTypeRegistration
    {
        public void Register(INativeTypeRegistry registry)
        {
            registry.RegisterEvent<NativeEvent>();
        }
    }

    /// A hand-written policy whose answers a test sets.
    private struct ProbeEvent : IGameEvent
    {
        public int Value;
        public GameEventNotifyResult Notify;
        public GameEventResult RunLocally;
        public GameEventResult Invoke;

        public GameEventNotifyResult CanGameEventNotifyEcs(GameEventContextRegistry contexts) => Notify;
        public GameEventResult CanGameEventRunLocally(GameEventContextRegistry contexts) => RunLocally;
        public GameEventResult CanEcsInvokeGameEvent(GameEventContextRegistry contexts) => Invoke;
    }

    private static NativeMappedEventManager GetManager()
    {
        var container = new Container(rules =>
            rules.With(FactoryMethod.ConstructorWithResolvableArguments)
                .WithDefaultReuse(Reuse.Singleton)
                .WithUseInterpretation()
        );

        container.Register<DataSideChannel>();
        container.Register<GameEventContextRegistry>();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new FailOnErrorLoggerProvider()));
        container.RegisterInstance(loggerFactory);
        container.Register(typeof(ILogger<>), typeof(Logger<>), ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        container.RegisterInstance(loggerFactory.CreateLogger("Tests"));

        container.Register<INativeTypeRegistration, TestEventsRegistration>();
        container.Register<INativeTypeRegistry, NativeTypeRegistry>();
        container.Register<NativeMappedEventManager>();

        container.RegisterInstance(new EntityStore());
        container.RegisterMany<Store>(nonPublicServiceTypes: true);

        return container.Resolve<NativeMappedEventManager>();
    }

    [Fact]
    public void ReactsToEcsEvents()
    {
        var manager = GetManager();
        var ecsHandled = false;

        manager.RegisterEcsEventHandler<ManagedEvent>(ev =>
        {
            Assert.Equal(5, ev.IntValue);
            ecsHandled = true;
        });
        manager.RegisterGameEventHandler<ManagedEvent>(_ => Assert.Fail());

        var result = manager.NotifyEcsIfApplicable(new ManagedEvent { IntValue = 5, FloatValue = 0.0f });

        Assert.True(ecsHandled);
        Assert.Equal(GameEventResult.RunAll, result);
    }

    [Fact]
    public void ReactsToGameEvents()
    {
        var manager = GetManager();
        var handled = false;

        manager.RegisterGameEventHandler<ManagedEvent>(ev =>
        {
            Assert.Equal(5, ev.IntValue);
            handled = true;
        });
        manager.RegisterEcsEventHandler<ManagedEvent>(_ => Assert.Fail());

        var result = manager.InvokeInGameIfApplicable(new ManagedEvent { IntValue = 5, FloatValue = 0.0f });

        Assert.True(handled);
        Assert.Equal(GameEventResult.RunAll, result);
    }

    [Fact]
    public void ReactsToBothEvents()
    {
        var manager = GetManager();
        var ecsHandled = false;
        var gameHandled = false;

        manager.RegisterEcsEventHandler<ManagedEvent>(ev =>
        {
            Assert.Equal(5, ev.IntValue);
            ecsHandled = true;
        });
        manager.RegisterGameEventHandler<ManagedEvent>(ev =>
        {
            Assert.Equal(5, ev.IntValue);
            gameHandled = true;
        });

        manager.InvokeInGameAndNotifyEcs(new ManagedEvent { IntValue = 5, FloatValue = 0.0f });

        Assert.True(ecsHandled);
        Assert.True(gameHandled);
    }

    [Fact]
    public void ReactsToNativeEcsEvents()
    {
        var manager = GetManager();
        var ecsHandled = false;

        manager.RegisterEcsEventHandler<NativeEvent>(ev =>
        {
            Assert.Equal(IntPtr.Zero, ev.Actor);
            Assert.Equal(5, ev.IntValue);
            ecsHandled = true;
        });
        manager.RegisterGameEventHandler<NativeEvent>(_ => Assert.Fail());

        var evPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeEvent>());
        try
        {
            Marshal.StructureToPtr(new NativeEvent { Actor = IntPtr.Zero, IntValue = 5 }, evPtr, false);
            var result = manager.NotifyEcsIfApplicable(NativeEvent.Id, evPtr);
            Assert.Equal((byte)GameEventResult.RunAll, result);
        }
        finally
        {
            Marshal.FreeHGlobal(evPtr);
        }

        Assert.True(ecsHandled);
    }

    [Fact]
    public void ReactsToNativeEcsEventsAsManaged()
    {
        var manager = GetManager();
        var ecsHandled = false;

        manager.RegisterEcsEventHandler<NativeEvent>(ev =>
        {
            Assert.Equal(5, ev.IntValue);
            ecsHandled = true;
        });
        manager.RegisterGameEventHandler<NativeEvent>(_ => Assert.Fail());

        manager.NotifyEcsIfApplicable(new NativeEvent { Actor = IntPtr.Zero, IntValue = 5 });

        Assert.True(ecsHandled);
    }

    [Fact]
    public void AnUnknownNativeEventIdIsAnError()
    {
        var manager = GetManager();

        Assert.ThrowsAny<Exception>(() => manager.NotifyEcsIfApplicable(NativeEvent.Id + 100, IntPtr.Zero));
        Assert.ThrowsAny<Exception>(() => manager.InvokeInGameIfApplicable(NativeEvent.Id + 100, IntPtr.Zero));
    }

    [Theory]
    [InlineData(GameEventNotifyResult.Notify, GameEventResult.RunAll, true)]
    [InlineData(GameEventNotifyResult.Notify, GameEventResult.Rejected, true)]
    [InlineData(GameEventNotifyResult.DontNotify, GameEventResult.RunAll, false)]
    [InlineData(GameEventNotifyResult.DontNotify, GameEventResult.Rejected, false)]
    public void NotifyAsksTheEventThenReturnsItsRunLocallyAnswer(GameEventNotifyResult notify, GameEventResult runLocally, bool notified)
    {
        var manager = GetManager();
        var ecsCalls = 0;
        manager.RegisterEcsEventHandler<ProbeEvent, object?>((_, _) => ecsCalls++, null);

        var result = manager.NotifyEcsIfApplicable(new ProbeEvent { Notify = notify, RunLocally = runLocally });

        Assert.Equal(notified ? 1 : 0, ecsCalls);
        Assert.Equal(runLocally, result);
    }

    [Theory]
    [InlineData(GameEventResult.RunAll, 1)]
    [InlineData(GameEventResult.RunOptimistic, 1)]
    [InlineData(GameEventResult.DontRun, 0)]
    [InlineData(GameEventResult.Rejected, 0)]
    public void InvokeRunsTheGameHandlersExactlyWhenTheEventSaysItRuns(GameEventResult invoke, int expectedCalls)
    {
        var manager = GetManager();
        var gameCalls = 0;
        manager.RegisterGameEventHandler<ProbeEvent, object?>((_, _) => gameCalls++, null);

        var result = manager.InvokeInGameIfApplicable(new ProbeEvent { Invoke = invoke });

        Assert.Equal(expectedCalls, gameCalls);
        Assert.Equal(invoke, result);
    }

    [Fact]
    public void WhilePropagatingToTheGameTheEventIsNotSentBackAndRunsLocally()
    {
        var manager = GetManager();
        var echo = new ProbeEvent { Notify = GameEventNotifyResult.Notify, RunLocally = GameEventResult.Rejected, Invoke = GameEventResult.RunAll };
        var ecsCalls = 0;
        GameEventResult? notifyResult = null;
        GameEventNotifyResult? canNotify = null;
        GameEventResult? canRun = null;

        manager.RegisterEcsEventHandler<ProbeEvent, object?>((_, _) => ecsCalls++, null);
        manager.RegisterGameEventHandler<ProbeEvent, object?>((_, _) =>
        {
            notifyResult = manager.NotifyEcsIfApplicable(echo);
            canNotify = manager.CanGameEventNotifyEcs(echo);
            canRun = manager.CanGameEventRunLocally(echo);
        }, null);

        manager.InvokeInGameIfApplicable(echo);

        Assert.Equal(0, ecsCalls);
        Assert.Equal(GameEventResult.RunAll, notifyResult);
        Assert.Equal(GameEventNotifyResult.DontNotify, canNotify);
        Assert.Equal(GameEventResult.RunAll, canRun);

        Assert.Equal(GameEventNotifyResult.Notify, manager.CanGameEventNotifyEcs(echo));
        Assert.Equal(GameEventResult.Rejected, manager.CanGameEventRunLocally(echo));
    }

    [Fact]
    public void WhilePropagatingToTheEcsTheEventIsNotPlayedBack()
    {
        var manager = GetManager();
        var echo = new ProbeEvent { Notify = GameEventNotifyResult.Notify, RunLocally = GameEventResult.RunAll, Invoke = GameEventResult.RunAll };
        var gameCalls = 0;
        GameEventResult? invokeResult = null;
        GameEventResult? canInvoke = null;

        manager.RegisterGameEventHandler<ProbeEvent, object?>((_, _) => gameCalls++, null);
        manager.RegisterEcsEventHandler<ProbeEvent, object?>((_, _) =>
        {
            invokeResult = manager.InvokeInGameIfApplicable(echo);
            canInvoke = manager.CanEcsInvokeGameEvent(echo);
        }, null);

        manager.NotifyEcsIfApplicable(echo);

        Assert.Equal(0, gameCalls);
        Assert.Equal(GameEventResult.DontRun, invokeResult);
        Assert.Equal(GameEventResult.DontRun, canInvoke);
        Assert.Equal(GameEventResult.RunAll, manager.CanEcsInvokeGameEvent(echo));
    }

    [Fact]
    public void NotifyingDoesNotAllocate()
    {
        var manager = GetManager();
        var sum = 0;
        manager.RegisterEcsEventHandler<ProbeEvent, object?>(static (ev, _) => { }, null);
        var ev = new ProbeEvent { Value = 1, Notify = GameEventNotifyResult.Notify, RunLocally = GameEventResult.RunAll };
        manager.NotifyEcsIfApplicable(ev);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            sum += (int)manager.NotifyEcsIfApplicable(ev);
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(3 * 1000, sum);
    }
}
