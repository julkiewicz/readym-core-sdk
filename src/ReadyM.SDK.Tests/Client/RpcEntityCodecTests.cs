using Friflo.Engine.ECS;
using LiteNetLib.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ReadyM.Api.DI;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.ECS.Managers;
using ReadyM.Api.Multiplayer.RPC;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Archetypes.Core;
using ReadyM.SDK.Client.Archetypes;
using ReadyM.SDK.Client.Entities;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Tests.Client.Fixtures;

namespace ReadyM.SDK.Tests.Client;

/// <summary>
/// What generated RPC code does with a shape parameter: writes its entity's network id, and on the other end finds the
/// entity by it and hands it over as the shape the handler takes, or says why it skipped the handler.
/// </summary>
public class RpcEntityCodecTests : IDisposable
{
    private readonly TestContainer _container;
    private readonly LocalPlayer _player = new();
    private readonly Warnings _warnings = new();

    public RpcEntityCodecTests()
    {
        _container = new TestContainer();
        _container.Init();

        _container.RegisterSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        _container.RegisterSingleton<ILogger>(_warnings);

        var store = new EntityStore();

        _container.RegisterSingleton(store);
        _container.RegisterSingleton(new Store(store, NullLogger.Instance, Array.Empty<IArchetypeRegistration>()));
        _container.RegisterSingleton<IPlayerIdProvider>(_player);
        _container.RegisterSingleton<INetworkedEntityManager, NetworkedEntityManager>();

        CreateHandlerRegistry.Use(_container);

        _container.RegisterSingleton<IEntityApi, ClientEntityApi>();
        _container.RegisterSingleton<IEntities, ClientEntities>();
        _container.RegisterSingleton<RpcEntities>();

        var world = _container.Resolve<Store>();
        world.SetThread(Thread.CurrentThread);

        Bind<Area>(world);
        Bind<Prop>(world);
        Bind<Rig>(world);
        Bind<Signpost>(world);

        ArchetypeBindings.Use(_bound);

        Entities = _container.Resolve<IEntities>();
        Rpc = new Handlers { RpcEntities = _container.Resolve<RpcEntities>() };
    }

    private IEntities Entities { get; }

    /// <summary>The handlers generated code would pass, given the world the way the host gives it.</summary>
    private RpcBase Rpc { get; }

    public void Dispose() => _container.Dispose();

    private void Bind<T>(Store world) where T : struct, IArchetype
    {
        var components = ArchetypeRegistry.SetFor(typeof(T), default(T).Components);
        var builder = new ArchetypeBuilder();

        foreach (var type in components.Types)
            builder.Add(type);

        _bound[typeof(T).FullName!] = world.RegisterArchetype(builder);
    }

    private readonly Dictionary<string, ArchetypeId> _bound = [];

    /// <summary>What the receiving end reads off a message one sender wrote.</summary>
    private static NetDataReader Sent(Action<NetDataWriter> write)
    {
        var writer = new NetDataWriter();
        write(writer);
        return new NetDataReader(writer.CopyData());
    }

    // -- what arrives --------------------------------------------------------------------------------

    [Fact]
    public void An_entity_arrives_as_the_same_entity()
    {
        var rig = Entities.Create<Rig>();
        var message = Sent(w => RpcEntityCodec.Write(w, rig, "rig"));

        Assert.True(RpcEntityCodec.TryRead(Rpc, message, out Rig found, "OnRig", "rig"));
        Assert.Equal(EntityHandle.Of(rig).RawEntity, EntityHandle.Of(found).RawEntity);
    }

    /// <summary>The receiver names the entity by what it needs of it, which a replicated mixin is enough for.</summary>
    [Fact]
    public void An_entity_arrives_as_a_mixin_it_carries()
    {
        var rig = Entities.Create<Rig>();
        var message = Sent(w => RpcEntityCodec.Write(w, rig, "rig"));

        Assert.True(RpcEntityCodec.TryRead(Rpc, message, out Telemetry found, "OnRig", "rig"));
        Assert.Equal(EntityHandle.Of(rig).RawEntity, EntityHandle.Of(found).RawEntity);
    }

    [Fact]
    public void Null_arrives_as_null_without_a_warning()
    {
        var message = Sent(w => RpcEntityCodec.WriteOptional(w, (Rig?)null, "rig"));

        Assert.True(RpcEntityCodec.TryReadOptional(Rpc, message, out Rig? found, "OnRig", "rig"));
        Assert.Null(found);
        Assert.Empty(_warnings.Messages);
    }

    [Fact]
    public void A_nullable_parameter_sent_with_an_entity_arrives_with_it()
    {
        var rig = Entities.Create<Rig>();
        var message = Sent(w => RpcEntityCodec.WriteOptional(w, (Rig?)rig, "rig"));

        Assert.True(RpcEntityCodec.TryReadOptional(Rpc, message, out Rig? found, "OnRig", "rig"));
        Assert.Equal(EntityHandle.Of(rig).RawEntity, EntityHandle.Of(found!.Value).RawEntity);
    }

    /// <summary>Each parameter reads only its own bytes, so the ones after it are where the sender put them.</summary>
    [Fact]
    public void The_payload_after_an_entity_is_read_as_it_was_written()
    {
        var rig = Entities.Create<Rig>();
        var reader = Sent(w =>
        {
            RpcEntityCodec.WriteOptional(w, (Rig?)null, "none");
            RpcEntityCodec.Write(w, rig, "rig");
            w.Put(42);
        });

        Assert.True(RpcEntityCodec.TryReadOptional(Rpc, reader, out Rig? _, "OnRig", "none"));
        Assert.True(RpcEntityCodec.TryRead(Rpc, reader, out Rig _, "OnRig", "rig"));
        Assert.Equal(42, reader.GetInt());
    }

    /// <summary>A handler skipped over one parameter still leaves the reader past it.</summary>
    [Fact]
    public void An_entity_that_is_not_found_is_still_read_past()
    {
        var rig = Entities.Create<Rig>();
        var reader = Sent(w =>
        {
            RpcEntityCodec.Write(w, rig, "rig");
            w.Put(42);
        });
        Entities.Delete(rig);

        Assert.False(RpcEntityCodec.TryRead(Rpc, reader, out Rig _, "OnRig", "rig"));
        Assert.Equal(42, reader.GetInt());
    }

    // -- what skips the handler ----------------------------------------------------------------------

    [Fact]
    public void An_entity_of_another_shape_skips_the_handler()
    {
        var rig = Entities.Create<Rig>();
        var message = Sent(w => RpcEntityCodec.Write(w, rig, "rig"));

        Assert.False(RpcEntityCodec.TryRead(Rpc, message, out Signpost _, "OnSign", "post"));
        Assert.Contains(_warnings.Messages, m => m.Contains("OnSign") && m.Contains("post") && m.Contains(nameof(Signpost)));
    }

    [Fact]
    public void An_entity_gone_by_the_time_it_arrives_skips_the_handler()
    {
        var rig = Entities.Create<Rig>();
        var message = Sent(w => RpcEntityCodec.Write(w, rig, "rig"));
        Entities.Delete(rig);

        Assert.False(RpcEntityCodec.TryRead(Rpc, message, out Rig _, "OnRig", "rig"));
        Assert.Single(_warnings.Messages);
    }

    [Fact]
    public void A_nullable_parameter_naming_a_missing_entity_skips_the_handler()
    {
        var rig = Entities.Create<Rig>();
        var message = Sent(w => RpcEntityCodec.WriteOptional(w, (Rig?)rig, "rig"));
        Entities.Delete(rig);

        Assert.False(RpcEntityCodec.TryReadOptional(Rpc, message, out Rig? found, "OnRig", "rig"));
        Assert.Null(found);
        Assert.Single(_warnings.Messages);
    }

    /// <summary>The generator never lets a required one be sent as null, so arriving as one is a broken message.</summary>
    [Fact]
    public void A_required_parameter_that_arrives_as_null_skips_the_handler()
    {
        var message = Sent(w => RpcEntityCodec.WriteOptional(w, (Rig?)null, "rig"));

        Assert.False(RpcEntityCodec.TryRead(Rpc, message, out Rig _, "OnRig", "rig"));
        Assert.Single(_warnings.Messages);
    }

    /// <summary>A host that forgot to set the world is told so, rather than getting a null reference.</summary>
    [Fact]
    public void Handlers_without_a_world_say_so()
    {
        var message = Sent(w => RpcEntityCodec.WriteOptional(w, (Rig?)null, "rig"));

        var thrown = Assert.Throws<InvalidOperationException>(() => RpcEntityCodec.TryRead(new Handlers(), message, out Rig _, "OnRig", "rig"));

        Assert.Contains("world", thrown.Message);
    }

    // -- what cannot be sent -------------------------------------------------------------------------

    [Fact]
    public void A_shape_with_no_entity_behind_it_cannot_be_sent()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => RpcEntityCodec.Write(new NetDataWriter(), default(Rig), "rig"));

        Assert.Contains("'rig'", thrown.Message);
    }

    [Fact]
    public void A_deleted_entity_cannot_be_sent()
    {
        var rig = Entities.Create<Rig>();
        Entities.Delete(rig);

        Assert.Throws<InvalidOperationException>(() => RpcEntityCodec.WriteOptional(new NetDataWriter(), (Rig?)rig, "rig"));
    }

    /// <summary>The generator refuses a shape like this in a contract; this is what it would have run into.</summary>
    [Fact]
    public void A_local_entity_cannot_be_sent()
        => Assert.Throws<InvalidOperationException>(() => RpcEntityCodec.Write(new NetDataWriter(), Entities.Create<Prop>(), "prop"));

    /// <summary>Handlers that never register for anything, which is all a read needs of them.</summary>
    private sealed class Handlers : RpcBase
    {
        protected override void InitRpc()
        {
        }

        protected override void DeInitRpc()
        {
        }
    }

    private sealed class LocalPlayer : IPlayerIdProvider
    {
        public PlayerId? PlayerId { get; } = new(7);
    }

    private sealed class TestContainer : DependencyContainerBase;

    private sealed class Warnings : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
                Messages.Add(formatter(state, exception));
        }
    }
}
