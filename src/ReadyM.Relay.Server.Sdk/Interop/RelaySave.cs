using System.Runtime.InteropServices;
using Friflo.Engine.ECS;
using ReadyM.Api.Multiplayer.Interop;
using ReadyM.Api.Saves;

namespace ReadyM.Relay.Server.Sdk.Interop;

/// The relay's save writer, reader and context as a mod component's own save code sees them. Each call
/// forwards to the relay, which holds the real ones for the duration of the save or load it is running.
internal sealed class RelaySave(SavePointers pointers)
{
    public RelaySaveWriter Writer { get; } = new(pointers);
    public RelaySaveReader Reader { get; } = new(pointers);
    public RelaySaveContext Context { get; } = new(pointers);
}

internal sealed unsafe class RelaySaveWriter(SavePointers pointers) : ISaveWriter
{
    private readonly SaveStepDelegate _beginObject = Marshal.GetDelegateForFunctionPointer<SaveStepDelegate>(pointers.BeginObject);
    private readonly SaveStepDelegate _endObject = Marshal.GetDelegateForFunctionPointer<SaveStepDelegate>(pointers.EndObject);
    private readonly SaveBeginArrayDelegate _beginArray = Marshal.GetDelegateForFunctionPointer<SaveBeginArrayDelegate>(pointers.BeginArray);
    private readonly SaveStepDelegate _endArray = Marshal.GetDelegateForFunctionPointer<SaveStepDelegate>(pointers.EndArray);
    private readonly SaveNameDelegate _name = Marshal.GetDelegateForFunctionPointer<SaveNameDelegate>(pointers.Name);
    private readonly SaveWriteValueDelegate _writeValue = Marshal.GetDelegateForFunctionPointer<SaveWriteValueDelegate>(pointers.WriteValue);
    private readonly SaveWriteStringDelegate _writeString = Marshal.GetDelegateForFunctionPointer<SaveWriteStringDelegate>(pointers.WriteString);

    public void BeginObject() => _beginObject();
    public void EndObject() => _endObject();
    public void BeginArray(int count) => _beginArray(count);
    public void EndArray() => _endArray();

    public void Name(string name)
    {
        fixed (char* chars = name)
            _name(chars, name.Length);
    }

    public void Write(bool value) => _writeValue((byte)SaveValueKind.Bool, value ? 1UL : 0UL);
    public void Write(byte value) => _writeValue((byte)SaveValueKind.Byte, value);
    public void Write(sbyte value) => _writeValue((byte)SaveValueKind.SByte, unchecked((ulong)value));
    public void Write(short value) => _writeValue((byte)SaveValueKind.Short, unchecked((ulong)value));
    public void Write(ushort value) => _writeValue((byte)SaveValueKind.UShort, value);
    public void Write(int value) => _writeValue((byte)SaveValueKind.Int, unchecked((ulong)value));
    public void Write(uint value) => _writeValue((byte)SaveValueKind.UInt, value);
    public void Write(long value) => _writeValue((byte)SaveValueKind.Long, unchecked((ulong)value));
    public void Write(ulong value) => _writeValue((byte)SaveValueKind.ULong, value);
    public void Write(float value) => _writeValue((byte)SaveValueKind.Float, BitConverter.SingleToUInt32Bits(value));
    public void Write(double value) => _writeValue((byte)SaveValueKind.Double, BitConverter.DoubleToUInt64Bits(value));

    public void Write(string value)
    {
        fixed (char* chars = value)
            _writeString(chars, value.Length);
    }
}

internal sealed unsafe class RelaySaveReader(SavePointers pointers) : ISaveReader
{
    private readonly SaveStepDelegate _beginObject = Marshal.GetDelegateForFunctionPointer<SaveStepDelegate>(pointers.ReadBeginObject);
    private readonly SaveStepDelegate _endObject = Marshal.GetDelegateForFunctionPointer<SaveStepDelegate>(pointers.ReadEndObject);
    private readonly SaveStepDelegate _beginArray = Marshal.GetDelegateForFunctionPointer<SaveStepDelegate>(pointers.ReadBeginArray);
    private readonly SaveStepDelegate _endArray = Marshal.GetDelegateForFunctionPointer<SaveStepDelegate>(pointers.ReadEndArray);
    private readonly SaveHasMoreElementsDelegate _hasMoreElements = Marshal.GetDelegateForFunctionPointer<SaveHasMoreElementsDelegate>(pointers.HasMoreElements);
    private readonly SaveNameDelegate _name = Marshal.GetDelegateForFunctionPointer<SaveNameDelegate>(pointers.ReadName);
    private readonly SaveTryNameDelegate _tryName = Marshal.GetDelegateForFunctionPointer<SaveTryNameDelegate>(pointers.TryName);
    private readonly SaveReadValueDelegate _readValue = Marshal.GetDelegateForFunctionPointer<SaveReadValueDelegate>(pointers.ReadValue);
    private readonly SaveReadStringDelegate _readString = Marshal.GetDelegateForFunctionPointer<SaveReadStringDelegate>(pointers.ReadString);
    private readonly SaveCopyStringDelegate _copyString = Marshal.GetDelegateForFunctionPointer<SaveCopyStringDelegate>(pointers.CopyString);

    public void BeginObject() => _beginObject();
    public void EndObject() => _endObject();
    public void BeginArray() => _beginArray();
    public bool HasMoreElements() => _hasMoreElements() != 0;
    public void EndArray() => _endArray();

    public void Name(string name)
    {
        fixed (char* chars = name)
            _name(chars, name.Length);
    }

    public bool TryName(string name)
    {
        fixed (char* chars = name)
            return _tryName(chars, name.Length) != 0;
    }

    public bool ReadBool() => _readValue((byte)SaveValueKind.Bool) != 0;
    public byte ReadByte() => (byte)_readValue((byte)SaveValueKind.Byte);
    public sbyte ReadSByte() => unchecked((sbyte)_readValue((byte)SaveValueKind.SByte));
    public short ReadShort() => unchecked((short)_readValue((byte)SaveValueKind.Short));
    public ushort ReadUShort() => (ushort)_readValue((byte)SaveValueKind.UShort);
    public int ReadInt() => unchecked((int)_readValue((byte)SaveValueKind.Int));
    public uint ReadUInt() => (uint)_readValue((byte)SaveValueKind.UInt);
    public long ReadLong() => unchecked((long)_readValue((byte)SaveValueKind.Long));
    public ulong ReadULong() => _readValue((byte)SaveValueKind.ULong);
    public float ReadFloat() => BitConverter.UInt32BitsToSingle((uint)_readValue((byte)SaveValueKind.Float));
    public double ReadDouble() => BitConverter.UInt64BitsToDouble(_readValue((byte)SaveValueKind.Double));

    public string ReadString()
    {
        var length = _readString();
        if (length == 0)
            return string.Empty;

        var buffer = new char[length];
        fixed (char* chars = buffer)
            _copyString(chars);

        return new string(buffer);
    }
}

/// <remarks>
/// A mod component references another entity by its <see cref="RawEntity"/>: a Friflo <see cref="Entity"/>
/// belongs to the relay's store, which this side does not hold.
/// </remarks>
internal sealed class RelaySaveContext(SavePointers pointers) : ISaveWriteContext, ISaveLoadContext
{
    private readonly SaveWriteEntityRefDelegate _writeEntityRef = Marshal.GetDelegateForFunctionPointer<SaveWriteEntityRefDelegate>(pointers.WriteEntityRef);
    private readonly SaveReadEntityRefDelegate _readEntityRef = Marshal.GetDelegateForFunctionPointer<SaveReadEntityRefDelegate>(pointers.ReadEntityRef);
    private readonly SaveVersionDelegate _version = Marshal.GetDelegateForFunctionPointer<SaveVersionDelegate>(pointers.Version);

    public ushort Version => _version();

    /// Writes a reference to another entity as its stable PersistentId, an empty one when it is not persisted.
    public void WriteEntityRef(RawEntity entity) => _writeEntityRef(entity);

    /// Reads a reference to another entity, default when empty or unresolved.
    public RawEntity ReadEntityRef() => _readEntityRef();

    void ISaveWriteContext.WriteEntityRef(ISaveWriter writer, Entity entity)
        => throw new NotSupportedException(
            "A mod component cannot hold a Friflo Entity, which belongs to the relay's store. It references "
            + "another entity by its RawEntity.");

    Entity ISaveLoadContext.ReadEntityRef(ISaveReader reader)
        => throw new NotSupportedException(
            "A mod component cannot hold a Friflo Entity, which belongs to the relay's store. It references "
            + "another entity by its RawEntity.");
}
