using System;
using System.Runtime.InteropServices;

namespace ReadyM.Api.Multiplayer.Interop;

/// <summary>The networked pointers of one mod component, as they cross from the managed mod side.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ModNetworkedComponentInfo
{
    public IntPtr WriteSnapshot;    // WriteSnapshotDelegate
    public IntPtr ReadSnapshot;     // ReadSnapshotDelegate
    public IntPtr WriteDelta;       // WriteDeltaDelegate
    public IntPtr ReadDelta;        // ReadDeltaDelegate
    public IntPtr ChangedFromApi;   // ChangedFromApiDelegate
}
