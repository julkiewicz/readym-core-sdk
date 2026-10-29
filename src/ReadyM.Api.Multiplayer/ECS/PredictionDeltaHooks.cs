using System;
using ReadyM.Api.Multiplayer.ECS.Components;

// ReSharper disable StaticMemberInGenericType

namespace ReadyM.Api.Multiplayer.ECS;

/// <summary>
/// Allows subscribing to the delta packing/unpacking process for a component type.
/// Used by the prediction system.
/// </summary>
internal static class PredictionDeltaHooks<T> where T : struct, INetworkedComponent
{
    public delegate void DeltaHook(ref T component);

    /// <summary>
    /// Before the server's delta is read onto the component.
    /// </summary>
    public static DeltaHook? BeforeDeltaRead;

    /// <summary>After the delta has been read.</summary>
    public static DeltaHook? AfterDeltaRead;

    /// <summary>Before an owner's changed fields go out.</summary>
    public static Action? BeforeDeltaSend;
}