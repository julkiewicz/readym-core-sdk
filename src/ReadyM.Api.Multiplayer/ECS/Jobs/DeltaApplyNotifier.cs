using System;

namespace ReadyM.Api.Multiplayer.ECS.Jobs;

/// <summary>
/// Says when a delta landed on an entity, for anything holding a value in front of the replicated one.
/// <see cref="ApplyDeltaJob{T}"/> is the only thing that knows a component arrived from the server rather than
/// being written here, which is what tells an answered guess from one still waiting.
/// </summary>
/// <remarks>
/// Friflo's own component events are no use for this: they fire on a component being added or removed, and a
/// delta is written into the component in place, so they would not fire at all for the ordinary case.
/// </remarks>
internal static class DeltaApplyNotifier
{
    private static Action<Type, int>? _watching;

    /// <summary>Whether anything cares, so the apply path costs a field read when nothing does.</summary>
    public static bool Watched => _watching is not null;

    /// <summary>Called once by whatever holds guesses, as it starts.</summary>
    public static void Watch(Action<Type, int> landed) => _watching += landed;

    /// <summary>Called by the apply job, once per entity a delta of this component landed on.</summary>
    public static void Landed(Type component, int entity) => _watching?.Invoke(component, entity);
}
