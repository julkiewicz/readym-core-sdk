using System;
using Friflo.Engine.ECS;

namespace ReadyM.Api.Multiplayer.Interop;

// A mod component's own save code, called by the relay. It takes no writer or context: the mod side
// passes its own, which forward to the relay's for the duration of the call.
internal delegate void WriteSaveRecordDelegate(IntPtr heapSelf, int index);

internal delegate void ReadSaveRecordDelegate(IntPtr heapSelf, int index);

// What the relay exports for that forwarding. Primitives cross as a kind plus their bits, so one pointer
// covers every ISaveWriter.Write and ISaveReader.Read overload.
internal delegate void SaveStepDelegate();

internal delegate void SaveBeginArrayDelegate(int count);

internal unsafe delegate void SaveNameDelegate(char* name, int length);

internal unsafe delegate byte SaveTryNameDelegate(char* name, int length);

internal delegate byte SaveHasMoreElementsDelegate();

internal delegate void SaveWriteValueDelegate(byte kind, ulong bits);

internal delegate ulong SaveReadValueDelegate(byte kind);

internal unsafe delegate void SaveWriteStringDelegate(char* value, int length);

/// Reads the current value as a string and holds it, returning its length, so the caller can size a
/// buffer for <see cref="SaveCopyStringDelegate"/>.
internal delegate int SaveReadStringDelegate();

internal unsafe delegate void SaveCopyStringDelegate(char* destination);

internal delegate void SaveWriteEntityRefDelegate(RawEntity entity);

internal delegate RawEntity SaveReadEntityRefDelegate();

internal delegate ushort SaveVersionDelegate();

/// What a primitive is, as it crosses in <see cref="SaveWriteValueDelegate"/> and
/// <see cref="SaveReadValueDelegate"/>.
internal enum SaveValueKind : byte
{
    Bool,
    Byte,
    SByte,
    Short,
    UShort,
    Int,
    UInt,
    Long,
    ULong,
    Float,
    Double,
}
