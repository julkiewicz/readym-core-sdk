using ReadyM.SDK.Tests.Server.Fixtures;

namespace ReadyM.SDK.Tests.Server;

/// <summary>A mixin's computed properties read and write the including archetype's own entity.</summary>
public class ServerComputedTests : ServerSdkTest
{
    [Fact]
    public void An_including_archetype_reads_them_from_its_entity()
    {
        var runner = Spawn<Runner>();

        runner.Max = 10;
        runner.Value = 0;
        Assert.True(runner.IsSpent);

        runner.Value = 4;
        Assert.False(runner.IsSpent);
        Assert.Equal(6, runner.Missing);
    }

    [Fact]
    public void Setting_one_writes_the_values_behind_it()
    {
        var runner = Spawn<Runner>();
        runner.Max = 10;

        runner.Missing = 3;

        Assert.Equal(7, runner.Value);
    }

    [Fact]
    public void A_query_loop_reads_them_for_each_entity()
    {
        Spawn<Runner>().Value = 0;
        Spawn<Runner>().Value = 5;

        var spent = 0;
        foreach (var runner in Entities.Query<Runner>())
            if (runner.IsSpent)
                spent++;

        Assert.Equal(1, spent);
    }
}
