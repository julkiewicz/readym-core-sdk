using System.Text.Json.Nodes;
using Friflo.Engine.ECS;
using ReadyM.Api.Saves;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Tests.Client.Fixtures;
using Yooni.Native.Container;

namespace ReadyM.SDK.Tests.Client;

/// What [Persisted] makes of a shape's component, and what it tells the SDK.
public class PersistenceTests
{
    [Fact]
    public void A_persisted_shape_makes_its_component_savable_under_its_name()
    {
        ISavableComponent component = default(HoardComponent);

        Assert.Equal("Test.Hoard", component.SaveName);
        Assert.Equal(1, component.SaveVersion);
    }

    [Fact]
    public void A_persisted_shape_stamps_the_version_it_declares()
    {
        ISavableComponent component = default(StashComponent);

        Assert.Equal("Test.Stash", component.SaveName);
        Assert.Equal(3, component.SaveVersion);
    }

    [Fact]
    public void A_component_round_trips_through_its_own_save_code()
    {
        var source = new HoardComponent { coins = 42, weight = 1.5f, label = new NativeString256("gold", false) };

        var writer = new TreeSaveWriter();
        source.WriteSave(writer, new NoEntityContext());

        var loaded = default(HoardComponent);
        loaded.ReadSave(new TreeSaveReader(writer.Root!), new NoEntityContext());

        Assert.Equal(42, loaded.coins);
        Assert.Equal(1.5f, loaded.weight);
        Assert.Equal("gold", loaded.label.ToString());
    }

    [Fact]
    public void Registering_the_assembly_registers_every_persisted_shape()
    {
        PersistenceRegistry.Clear();

        Assert.False(PersistenceRegistry.TryGet(typeof(HoardComponent), out _));

        global::ReadyM.SDK.Generated.ShapeRegistrations.RegisterAll();

        Assert.True(PersistenceRegistry.TryGet(typeof(HoardComponent), out _));
        Assert.True(PersistenceRegistry.TryGet(typeof(StashComponent), out _));
        Assert.False(PersistenceRegistry.TryGet(typeof(ClimateComponent), out _));
    }

    [Fact]
    public void A_persisted_component_is_handed_back_with_its_type()
    {
        global::ReadyM.SDK.Generated.ShapeRegistrations.RegisterAll();
        Assert.True(PersistenceRegistry.TryGet(typeof(HoardComponent), out var persisted));

        var callback = new RecordingCallback();
        persisted.Accept(callback);

        Assert.Equal(typeof(HoardComponent), callback.Accepted);
    }

    private sealed class RecordingCallback : IPersistedComponentCallback
    {
        public Type? Accepted { get; private set; }

        public void Accept<T>() where T : struct, ISavableComponent => Accepted = typeof(T);
    }

    private sealed class NoEntityContext : ISaveWriteContext, ISaveLoadContext
    {
        public ushort Version => 1;

        public void WriteEntityRef(ISaveWriter writer, Entity entity) => throw new NotSupportedException();

        public Entity ReadEntityRef(ISaveReader reader) => throw new NotSupportedException();
    }

    /// Builds the record as a JSON tree, which is all a component's own save code can tell apart.
    private sealed class TreeSaveWriter : ISaveWriter
    {
        private readonly Stack<JsonNode> _open = new();
        private string? _name;

        public JsonNode? Root { get; private set; }

        private void Put(JsonNode? value)
        {
            if (_open.Count == 0)
            {
                Root = value;
                return;
            }

            switch (_open.Peek())
            {
                case JsonObject obj:
                    obj[_name!] = value;
                    break;
                case JsonArray array:
                    array.Add(value);
                    break;
            }
        }

        private void Open(JsonNode node)
        {
            Put(node);
            _open.Push(node);
        }

        public void BeginObject() => Open(new JsonObject());
        public void EndObject() => _open.Pop();
        public void BeginArray(int count) => Open(new JsonArray());
        public void EndArray() => _open.Pop();
        public void Name(string name) => _name = name;
        public void Write(bool value) => Put(value);
        public void Write(byte value) => Put(value);
        public void Write(sbyte value) => Put(value);
        public void Write(short value) => Put(value);
        public void Write(ushort value) => Put(value);
        public void Write(int value) => Put(value);
        public void Write(uint value) => Put(value);
        public void Write(long value) => Put(value);
        public void Write(ulong value) => Put(value);
        public void Write(float value) => Put(value);
        public void Write(double value) => Put(value);
        public void Write(string value) => Put(value);
    }

    private sealed class TreeSaveReader(JsonNode root) : ISaveReader
    {
        private readonly Stack<JsonNode> _open = new();
        private readonly Stack<int> _indices = new();
        private JsonNode? _current = root;

        public void BeginObject() => _open.Push(_current!);
        public void EndObject() => _open.Pop();

        public void BeginArray()
        {
            _open.Push(_current!);
            _indices.Push(-1);
        }

        public bool HasMoreElements()
        {
            var array = (JsonArray)_open.Peek();
            var next = _indices.Pop() + 1;
            _indices.Push(next);

            if (next >= array.Count)
                return false;

            _current = array[next];
            return true;
        }

        public void EndArray()
        {
            _open.Pop();
            _indices.Pop();
        }

        public void Name(string name) => _current = ((JsonObject)_open.Peek())[name];

        public bool TryName(string name)
        {
            if (!((JsonObject)_open.Peek()).TryGetPropertyValue(name, out var value))
                return false;

            _current = value;
            return true;
        }

        public bool ReadBool() => _current!.GetValue<bool>();
        public byte ReadByte() => _current!.GetValue<byte>();
        public sbyte ReadSByte() => _current!.GetValue<sbyte>();
        public short ReadShort() => _current!.GetValue<short>();
        public ushort ReadUShort() => _current!.GetValue<ushort>();
        public int ReadInt() => _current!.GetValue<int>();
        public uint ReadUInt() => _current!.GetValue<uint>();
        public long ReadLong() => _current!.GetValue<long>();
        public ulong ReadULong() => _current!.GetValue<ulong>();
        public float ReadFloat() => _current!.GetValue<float>();
        public double ReadDouble() => _current!.GetValue<double>();
        public string ReadString() => _current!.GetValue<string>();
    }
}
