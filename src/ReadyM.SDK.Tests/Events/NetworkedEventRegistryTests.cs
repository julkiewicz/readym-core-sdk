using System.Collections.Generic;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyM.Api.DI;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.Api.Multiplayer.RPC;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Client.Events;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Events;
using ReadyM.SDK.Server.Events;
using Xunit;

namespace ReadyM.SDK.Tests.Events;

/// <summary>
/// What a host gets out of the registry once the mods are in: one coordinator per declared event, built by the
/// container from every handler set this side declared for it. Generated code never names a coordinator, so
/// this is the only place the two halves meet.
/// </summary>
public class NetworkEventRegistryTests
{
    private readonly List<string> _ran = [];

    private struct Knock : INetworkEvent
    {
        public RelayMessageCode Code => RelayMessageCode.MinServerRpcEvent;

        public void Serialize(NetDataWriter writer)
        {
        }
    }

    private sealed class Contract : INetworkEventContract<Knock>
    {
        public RelayMessageCode Code => RelayMessageCode.MinServerRpcEvent;

        public Scope Scope => Scope.Server;

        public Policy Policy => Policy.Anyone;

        public EntityHandle? Subject(in Knock networkEvent) => null;

        public PlayerId Target(in Knock networkEvent) => PlayerId.Invalid;

        public bool TryRead(NetworkEventDispatchCore world, NetDataReader reader, out Knock networkEvent)
        {
            networkEvent = default;

            return true;
        }

        public void WriteReason(NetDataWriter writer, object? reason)
        {
        }

        public object? ReadReason(NetDataReader reader) => null;
    }

    /// <summary>
    /// Stands in for what a mod generates. The container builds it, so it takes what it needs the way a real
    /// one takes its handler classes.
    /// </summary>
    private abstract class Handlers(Ran ran) : INetworkEventHandlers<Knock>
    {
        public abstract string Mod { get; }

        public IReadOnlyList<string> After => [];

        public void Run(Stage stage, ref Knock networkEvent, EventContext context)
        {
            if (stage == Stage.Apply)
                ran.Steps.Add(Mod);
        }
    }

    private sealed class Owner(Ran ran) : Handlers(ran)
    {
        public override string Mod => "Owner";
    }

    private sealed class Rules(Ran ran) : Handlers(ran)
    {
        public override string Mod => "Rules";
    }

    /// <summary>Registered so the container can hand the same list to whatever it builds.</summary>
    private sealed class Ran(List<string> steps)
    {
        public List<string> Steps { get; } = steps;
    }

    private sealed class Container : DependencyContainerBase;

    private Container Registered(RpcSide side, INetworkEventCoordinators coordinators)
    {
        var container = new Container();

        container.Init();
        container.RegisterSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        container.RegisterSingleton<ILogger>(NullLogger.Instance);
        container.RegisterSingleton(new Ran(_ran));

        NetworkEventRegistry.Declare<Knock>(new Contract());
        NetworkEventRegistry.Handle<Owner, Knock>(RpcSide.Server);
        NetworkEventRegistry.Handle<Rules, Knock>(RpcSide.Server);
        NetworkEventRegistry.RegisterAll(container, side, coordinators);

        return container;
    }

    private Container OnServer() => Registered(RpcSide.Server, new ServerNetworkEventCoordinators());

    [Fact]
    public void A_declared_event_gets_one_coordinator_the_container_can_build()
    {
        using var container = OnServer();

        Assert.Same(
            container.Resolve<INetworkEventDispatch<Knock>>(), container.Resolve<INetworkEventDispatch<Knock>>());
    }

    /// <summary>
    /// The whole point of one coordinator rather than one dispatcher per mod: a raise reaches every mod's
    /// handlers. Resolving a dispatcher per mod handed back whichever one the container happened to pick.
    /// </summary>
    [Fact]
    public void A_raise_reaches_every_mods_handlers()
    {
        using var container = OnServer();

        container.Resolve<INetworkEventDispatch<Knock>>().Raise(default, PlayerId.Server, true);

        Assert.Equal(["Owner", "Rules"], _ran);
    }

    /// <summary>Starting it is what makes it listen, so a host that starts its services must be able to.</summary>
    [Fact]
    public void A_coordinator_starts_with_the_containers_other_services()
    {
        using var container = OnServer();

        container.StartHostedServices();

        Assert.NotNull(container.Resolve<INetworkEventDispatch<Knock>>());
    }

    /// <summary>
    /// A module initializer runs the declarations where the target has one, the mod loader runs them for every
    /// assembly it loads, and a host may say it again once the last mod is in.
    /// </summary>
    [Fact]
    public void Registering_everything_twice_still_raises_each_handler_once()
    {
        using var container = OnServer();

        NetworkEventRegistry.RegisterAll(container, RpcSide.Server, new ServerNetworkEventCoordinators());
        container.Resolve<INetworkEventDispatch<Knock>>().Raise(default, PlayerId.Server, true);

        Assert.Equal(["Owner", "Rules"], _ran);
    }

    /// <summary>
    /// A client gets a coordinator for the same event, from the same declaration, because the Common project
    /// that declared it is on both machines. It has no server handler set in it: a Validate running on a
    /// client is the thing the stages exist to prevent.
    /// </summary>
    [Fact]
    public void A_client_gets_a_coordinator_without_the_servers_handlers()
    {
        using var container = Registered(RpcSide.Client, new ClientNetworkEventCoordinators());

        container.StartHostedServices();

        Assert.NotNull(container.Resolve<ClientNetworkEventCoordinator<Knock>>());
        Assert.Empty(container.ResolveAll<INetworkEventHandlers<Knock>>());
    }
}
