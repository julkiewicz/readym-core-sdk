using System.Runtime.InteropServices;

namespace ReadyM.Relay.Server.Sdk.Interop;

/// The relay's save writer, reader and context, for a mod component's own save code to write through.
/// <exclude />
[StructLayout(LayoutKind.Sequential)]
public struct SavePointers
{
    public required IntPtr BeginObject;
    public required IntPtr EndObject;
    public required IntPtr BeginArray;
    public required IntPtr EndArray;
    public required IntPtr Name;
    public required IntPtr WriteValue;
    public required IntPtr WriteString;
    public required IntPtr WriteEntityRef;

    public required IntPtr ReadBeginObject;
    public required IntPtr ReadEndObject;
    public required IntPtr ReadBeginArray;
    public required IntPtr ReadEndArray;
    public required IntPtr HasMoreElements;
    public required IntPtr ReadName;
    public required IntPtr TryName;
    public required IntPtr ReadValue;
    public required IntPtr ReadString;
    public required IntPtr CopyString;
    public required IntPtr ReadEntityRef;
    public required IntPtr Version;
}
