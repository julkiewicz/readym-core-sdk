using ReadyM.Api.Mapping.Events;

namespace ReadyM.Api.Tests;

public class GameEventTypesTests
{
    [Fact]
    public void GameEventResultHasTheByteValuesTheNativeSideMirrors()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(GameEventResult)));
        Assert.Equal(0x00, (byte)GameEventResult.DontRun);
        Assert.Equal(0x01, (byte)GameEventResult.RunOptimistic);
        Assert.Equal(0x02, (byte)GameEventResult.RunAuthoritative);
        Assert.Equal(0x04, (byte)GameEventResult.UndoOptimistic);
        Assert.Equal(0x80, (byte)GameEventResult.Rejected);
        Assert.Equal(0x03, (byte)GameEventResult.RunAll);
        Assert.Equal(6, Enum.GetValues<GameEventResult>().Length);
    }

    [Fact]
    public void GameEventNotifyResultHasTwoValues()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(GameEventNotifyResult)));
        Assert.Equal(0, (byte)GameEventNotifyResult.DontNotify);
        Assert.Equal(1, (byte)GameEventNotifyResult.Notify);
        Assert.Equal(2, Enum.GetValues<GameEventNotifyResult>().Length);
    }

    [Theory]
    [InlineData(GameEventResult.DontRun, false, false)]
    [InlineData(GameEventResult.RunOptimistic, true, false)]
    [InlineData(GameEventResult.RunAuthoritative, true, false)]
    [InlineData(GameEventResult.RunAll, true, false)]
    [InlineData(GameEventResult.UndoOptimistic, false, false)]
    [InlineData(GameEventResult.Rejected, false, true)]
    public void RunsAndIsRejectedReadTheFlags(GameEventResult result, bool runs, bool rejected)
    {
        Assert.Equal(runs, result.Runs());
        Assert.Equal(rejected, result.IsRejected());
    }

    private readonly struct FirstContext(int value)
    {
        public int Value { get; } = value;
    }

    private readonly struct SecondContext(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class Registration(Action<GameEventContextRegistry> register) : IGameEventContextRegistration
    {
        public void Register(GameEventContextRegistry registry) => register(registry);
    }

    [Fact]
    public void ContextRegistryHandsBackEachRegisteredContext()
    {
        var registry = new GameEventContextRegistry([
            new Registration(r => r.Register(new FirstContext(7))),
            new Registration(r => r.Register(new SecondContext("second"))),
        ]);

        Assert.Equal(7, registry.GetContext<FirstContext>().Value);
        Assert.Equal("second", registry.GetContext<SecondContext>().Name);
    }

    [Fact]
    public void ContextRegistriesAreIndependent()
    {
        var one = new GameEventContextRegistry([new Registration(r => r.Register(new FirstContext(1)))]);
        var two = new GameEventContextRegistry([new Registration(r => r.Register(new FirstContext(2)))]);

        Assert.Equal(1, one.GetContext<FirstContext>().Value);
        Assert.Equal(2, two.GetContext<FirstContext>().Value);
        Assert.Throws<InvalidOperationException>(() => two.GetContext<SecondContext>());
    }

    [Fact]
    public void ContextRegistryRefusesAMissingContext()
    {
        var registry = new GameEventContextRegistry([new Registration(r => r.Register(new FirstContext(1)))]);

        var error = Assert.Throws<InvalidOperationException>(() => registry.GetContext<SecondContext>());
        Assert.Contains(nameof(SecondContext), error.Message);
    }

    [Fact]
    public void ContextRegistryRefusesTheSameContextTwice()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new GameEventContextRegistry([
            new Registration(r => r.Register(new FirstContext(1))),
            new Registration(r => r.Register(new FirstContext(2))),
        ]));
        Assert.Contains(nameof(FirstContext), error.Message);
    }

    [Fact]
    public void GettingAContextDoesNotAllocate()
    {
        var registry = new GameEventContextRegistry([new Registration(r => r.Register(new FirstContext(3)))]);
        var sum = registry.GetContext<FirstContext>().Value;

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            sum += registry.GetContext<FirstContext>().Value;
        }
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(3 * 1001, sum);
    }
}
