using System;
using ReadyM.Api.Multiplayer.ECS.Components;

namespace ReadyM.Api.Multiplayer.ECS.Jobs;

/// <summary>
/// Where something holding a field of <typeparamref name="T"/> apart from the server's value hears about the
/// delta path: a client's guess at a value, while it waits for the server's answer. Each hook is null unless
/// something is held in this component type, which is almost always, so each path pays one null check.
/// </summary>
/// <remarks>
/// Per component type rather than one list for every type, so a delta for a component nothing is held in
/// pays nothing. Game thread only, like the entities the hooks are handed.
/// </remarks>
internal static class DeltaHooks<T> where T : struct, INetworkedComponent
{
    /// <summary>The component as it sits in the world, which tells one world's entity from another's.</summary>
    public delegate void Arrival(ref T component);

    /// <summary>
    /// Before the server's delta is read onto the component. Whatever is held is taken out first, so the read
    /// lands on the server's own values and a field the delta does not carry is the server's value still.
    /// </summary>
    // ReSharper disable once StaticMemberInGenericType
    public static Arrival? Landing;

    /// <summary>After the delta has been read, for whatever was held to see whether it was answered.</summary>
    // ReSharper disable once StaticMemberInGenericType
    public static Arrival? Landed;

    /// <summary>Before an owner's changed fields go out, for what is held to keep itself out of them.</summary>
    // ReSharper disable once StaticMemberInGenericType
    public static Action? Sending;
}
