using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace ReadyM.Api.ECS.Registry;

/// <summary>
/// A registry that hands out ids. Every component arrives before it seals, so the ids are handed out then, sorted by
/// the comparison it is given: never the order Register ran in.
/// </summary>
internal abstract class IdComponentRegistryBase<TRegistry, TComponent>
    : ComponentRegistryBase<TRegistry, TComponent>
    where TRegistry : IComponentRegistryBase<TRegistry, TComponent>
{
    /// <summary>
    /// What one registration passed to the innermost RegisterImpl, which is what the id order compares. A typed
    /// component's default value is left out: no order has a reason to read it.
    /// </summary>
    public readonly record struct RegisteredComponent(Type? Type, ModComponentInfo ModComponentInfo, string TypeFullName);

    private readonly List<RegisteredComponent> _registered = [];
    private readonly Dictionary<string, byte> _ids = new();
    private readonly List<string> _byId = [];

    protected IdComponentRegistryBase(
        IEnumerable<IComponentRegistrationBase<TRegistry, TComponent>> registrations,
        Comparison<RegisteredComponent> idOrder)
        : base(registrations)
    {
        // The base constructor ran every registration and sealed
        _registered.Sort(idOrder);

        foreach (var registered in _registered)
        {
            _ids.Add(registered.TypeFullName, (byte)_byId.Count);
            _byId.Add(registered.TypeFullName);
        }
    }

    /// <summary>Full name order, ordinal: the default, so the ids do not depend on one machine's registration order.</summary>
    protected IdComponentRegistryBase(IEnumerable<IComponentRegistrationBase<TRegistry, TComponent>> registrations)
        : this(registrations, (left, right) => string.CompareOrdinal(left.TypeFullName, right.TypeFullName))
    {
    }

    /// <summary>How many components have ids.</summary>
    protected int IdCount
    {
        get
        {
            ThrowIfNotSealed("its ids");
            return _byId.Count;
        }
    }

    protected byte GetId(string typeFullName)
    {
        ThrowIfNotSealed(typeFullName);
        return _ids[typeFullName];
    }

    protected string GetTypeFullName(int id)
    {
        ThrowIfNotSealed($"the component of id {id}");
        return _byId[id];
    }

    protected override TRegistry RegisterComponentImpl<T>(ValueTuple context, T defaultValue = default)
    {
        ThrowIfFull();
        var registry = base.RegisterComponentImpl(context, defaultValue);
        _registered.Add(new RegisteredComponent(typeof(T), default, typeof(T).FullName!));
        return registry;
    }

    protected override TRegistry RegisterModComponentImpl(ModComponentInfo info, string typeFullName)
    {
        ThrowIfFull();
        var registry = base.RegisterModComponentImpl(info, typeFullName);
        _registered.Add(new RegisteredComponent(null, info, typeFullName));
        return registry;
    }

    private void ThrowIfFull()
    {
        if (_registered.Count == byte.MaxValue)
        {
            throw new InvalidOperationException($"Cannot register more than {byte.MaxValue} components");
        }
    }
}
