using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyM.Api.DI;
using ReadyM.SDK.Client;
using ReadyM.SDK.Client.Systems;
using ReadyM.SDK.Services;

namespace ReadyM.SDK.Tests.Client;

/// What the mods declared reaches the container in its own step, which a host runs once their
/// assemblies are in and before any of their code does. Getting that order wrong used to leave a
/// [Service] unresolvable in a mod's entry point and never ticking afterwards, with nothing said.
public class ModDeclarationOrderTests
{
    [Fact]
    public void A_container_says_whether_the_declarations_reached_it()
    {
        using var container = Empty();

        Assert.False(ServiceRegistry.RegisteredFor(container));

        container.RegisterModDeclarations();

        Assert.True(ServiceRegistry.RegisteredFor(container));
    }

    /// Asked of each container on its own, because a process may hold more than one.
    [Fact]
    public void Registering_one_container_says_nothing_about_another()
    {
        using var registered = Empty();
        using var untouched = Empty();

        registered.RegisterModDeclarations();

        Assert.False(ServiceRegistry.RegisteredFor(untouched));
    }

    /// <summary>The updates system refuses a container the declarations never reached.</summary>
    /// <remarks>
    /// It used to register them itself, which made a Friflo system's constructor decide when a mod's
    /// [Service] became resolvable. That is invisible, and too late for an entry point that asks for
    /// one. Refusing says so where the host can fix it.
    /// </remarks>
    [Fact]
    public void The_updates_system_refuses_a_container_the_declarations_never_reached()
    {
        using var container = Empty();

        var refused = Assert.Throws<InvalidOperationException>(() => new ModSystemUpdates(container));

        Assert.Contains(nameof(DependencyInjectionExtensions.RegisterModDeclarations), refused.Message);
    }

    [Fact]
    public void The_updates_system_takes_a_container_they_reached()
    {
        using var container = Empty();

        container.RegisterModDeclarations();

        Assert.NotNull(new ModSystemUpdates(container));
    }

    private static Container Empty()
    {
        var container = new Container();

        container.Init();
        container.RegisterSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        container.RegisterSingleton<ILogger>(NullLogger.Instance);

        return container;
    }

    private sealed class Container : DependencyContainerBase;
}
