using System;
using System.Collections.Generic;
using System.Threading;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The contexts a game event's policy reads (ownership, master client, ...), one struct per type, registered once at
/// start-up. Looking one up is an array index and a type check: no dictionary and no box.
/// </summary>
public sealed class GameEventContextRegistry
{
    private sealed class Holder<TContext>(TContext value)
        where TContext : struct
    {
        public readonly TContext Value = value;
    }

    private static int _contextTypeCount;

    private static class ContextIndex<TContext>
        where TContext : struct
    {
        public static readonly int Value = Interlocked.Increment(ref _contextTypeCount) - 1;
    }

    private object?[] _holders = [];

    public GameEventContextRegistry(IEnumerable<IGameEventContextRegistration> registrations)
    {
        foreach (var registration in registrations)
        {
            registration.Register(this);
        }
    }

    public void Register<TContext>(in TContext context)
        where TContext : struct
    {
        var index = ContextIndex<TContext>.Value;
        if (index >= _holders.Length)
            Array.Resize(ref _holders, index + 1);

        if (_holders[index] != null)
            throw new InvalidOperationException($"Game event context {typeof(TContext).FullName} is registered twice");

        _holders[index] = new Holder<TContext>(context);
    }

    public ref readonly TContext GetContext<TContext>()
        where TContext : struct
    {
        var index = ContextIndex<TContext>.Value;
        if (index < _holders.Length && _holders[index] is Holder<TContext> holder)
            return ref holder.Value;

        throw new InvalidOperationException($"No game event context {typeof(TContext).FullName} is registered");
    }
}
