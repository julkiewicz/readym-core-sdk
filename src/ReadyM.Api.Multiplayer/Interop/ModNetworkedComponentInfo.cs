using System;
using LiteNetLib;

namespace ReadyM.Api.Multiplayer.Interop;

/// <summary>
/// What the relay needs to send and receive a networked component a mod defines: the mod's own serialization
/// callbacks, see <see cref="WriteSnapshotDelegate"/> and the rest, and how its changes travel.
/// </summary>
internal struct ModNetworkedComponentInfo
{
    /// <summary>Function pointer, a <see cref="WriteSnapshotDelegate"/>.</summary>
    public IntPtr WriteSnapshot;

    /// <summary>Function pointer, a <see cref="ReadSnapshotDelegate"/>.</summary>
    public IntPtr ReadSnapshot;

    /// <summary>Function pointer, a <see cref="WriteDeltaDelegate"/>.</summary>
    public IntPtr WriteDelta;

    /// <summary>Function pointer, a <see cref="ReadDeltaDelegate"/>.</summary>
    public IntPtr ReadDelta;

    /// <summary>Function pointer, a <see cref="ChangedFromApiDelegate"/>.</summary>
    public IntPtr ChangedFromApi;

    /// <summary>How the component's changes travel.</summary>
    public DeliveryMethod Delivery;
}
