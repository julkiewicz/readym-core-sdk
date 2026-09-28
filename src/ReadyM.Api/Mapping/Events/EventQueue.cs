using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ReadyM.Api.Mapping.Events;

internal class EventQueue(ILogger logger)
{
    private abstract class EntryBase
    {
        public abstract void Invoke(in object ev);
    }

    private abstract class EntryBase<TEvent> : EntryBase
    {
        public override void Invoke(in object ev)
        {
            if (ev is TEvent typedEv)
            {
                Invoke(typedEv);
            }
            else
            {
                throw new InvalidOperationException($"Invalid event type passed to entry. Expected {typeof(TEvent).FullName}, got {ev.GetType().FullName}");
            }
        }

        public abstract void Invoke(in TEvent ev);
    }

    private class Entry<TEvent, TArg>(ILogger logger) : EntryBase<TEvent>
    {
        private readonly List<(Action<TEvent, TArg>, TArg)> _handlers = new();

        public void RegisterHandler(Action<TEvent, TArg> handler, TArg arg)
        {
            _handlers.Add((handler, arg));
        }

        public override void Invoke(in TEvent ev)
        {
            if (_handlers.Count == 0)
            {
                logger.LogWarning("Invoking event of type {EventType} with no handlers registered", typeof(TEvent).FullName);
            }

            foreach (var (handler, arg) in _handlers)
            {
                handler(ev, arg);
            }
        }
    }

    private class OpaqueEntry<TArg>(ILogger logger) : EntryBase
    {
        private readonly List<(Action<object, TArg>, TArg)> _handlers = new();

        public void RegisterHandler(Action<object, TArg> handler, TArg arg)
        {
            _handlers.Add((handler, arg));
        }

        public override void Invoke(in object ev)
        {
            if (_handlers.Count == 0)
            {
                logger.LogWarning("Invoking event of type {EventType} with no handlers registered", ev.GetType().FullName);
            }

            foreach (var (handler, arg) in _handlers)
            {
                handler(ev, arg);
            }
        }
    }

    private class Entry<TEvent, TArg0, TArg1>(ILogger logger) : EntryBase<TEvent>
    {
        private readonly List<(Action<TEvent, TArg0, TArg1>, TArg0, TArg1)> _handlers = new();

        public void RegisterHandler(Action<TEvent, TArg0, TArg1> handler, TArg0 arg0, TArg1 arg1)
        {
            _handlers.Add((handler, arg0, arg1));
        }

        public override void Invoke(in TEvent ev)
        {
            if (_handlers.Count == 0)
            {
                logger.LogWarning("Invoking event of type {EventType} with no handlers registered", typeof(TEvent).FullName);
            }

            foreach (var (handler, arg0, arg1) in _handlers)
            {
                handler(ev, arg0, arg1);
            }
        }
    }

    private class Entry<TEvent, TArg0, TArg1, TArg2>(ILogger logger) : EntryBase<TEvent>
    {
        private readonly List<(Action<TEvent, TArg0, TArg1, TArg2>, TArg0, TArg1, TArg2)> _handlers = new();

        public void RegisterHandler(Action<TEvent, TArg0, TArg1, TArg2> handler, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            _handlers.Add((handler, arg0, arg1, arg2));
        }

        public override void Invoke(in TEvent ev)
        {
            if (_handlers.Count == 0)
            {
                logger.LogWarning("Invoking event of type {EventType} with no handlers registered", typeof(TEvent).FullName);
            }

            foreach (var (handler, arg0, arg1, arg2) in _handlers)
            {
                handler(ev, arg0, arg1, arg2);
            }
        }
    }

    // NOTE: One entry per registration, in registration order, so every handler runs exactly once per event.
    private readonly Dictionary<Type, List<EntryBase>> _entriesByEventType = new();

    private void AddEntry(Type eventType, EntryBase entry)
    {
        if (!_entriesByEventType.TryGetValue(eventType, out var entryList))
        {
            entryList = [];
            _entriesByEventType[eventType] = entryList;
        }

        entryList.Add(entry);
    }

    public void RegisterHandler<TEvent>(Action<TEvent> handler)
    {
        var entry = new Entry<TEvent, object?>(logger);
        entry.RegisterHandler((ev, _) => handler(ev), null);
        AddEntry(typeof(TEvent), entry);
    }

    public void RegisterHandler<TEvent, TArg>(Action<TEvent, TArg> handler, TArg arg)
    {
        var entry = new Entry<TEvent, TArg>(logger);
        entry.RegisterHandler(handler, arg);
        AddEntry(typeof(TEvent), entry);
    }

    public void RegisterOpaqueHandler<TArg>(Type eventType, Action<object, TArg> handler, TArg arg)
    {
        var entry = new OpaqueEntry<TArg>(logger);
        entry.RegisterHandler(handler, arg);
        AddEntry(eventType, entry);
    }

    public void RegisterHandler<TEvent, TArg0, TArg1>(Action<TEvent, TArg0, TArg1> handler, TArg0 arg0, TArg1 arg1)
    {
        var entry = new Entry<TEvent, TArg0, TArg1>(logger);
        entry.RegisterHandler(handler, arg0, arg1);
        AddEntry(typeof(TEvent), entry);
    }

    public void RegisterHandler<TEvent, TArg0, TArg1, TArg2>(Action<TEvent, TArg0, TArg1, TArg2> handler, TArg0 arg0, TArg1 arg1, TArg2 arg2)
    {
        var entry = new Entry<TEvent, TArg0, TArg1, TArg2>(logger);
        entry.RegisterHandler(handler, arg0, arg1, arg2);
        AddEntry(typeof(TEvent), entry);
    }

    public void Invoke<TEvent>(in TEvent ev)
    {
        if (_entriesByEventType.TryGetValue(typeof(TEvent), out var entryList))
        {
            foreach (var entry in entryList)
            {
                entry.Invoke(ev!);
            }
        }
    }

    public void Invoke(in object ev, Type eventType)
    {
        if (_entriesByEventType.TryGetValue(eventType, out var entryList))
        {
            foreach (var entry in entryList)
            {
                entry.Invoke(ev);
            }
        }
    }
}