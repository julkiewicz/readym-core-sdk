using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace ReadyM.Api.ECS.Registry;

/// <summary>
/// A registry that hands out ids. Every component arrives before it seals, so the ids are handed out then, sorted by
/// the comparison it is given: never the order Register ran in.
/// </summary>
internal abstract class IdComponentRegistryBase<TRegistry, TComponent, TContext>
    : ComponentRegistryBase<TRegistry, TComponent, TContext>
    where TRegistry : IComponentRegistryBase<TRegistry, TComponent>
{
    /// <summary>
    /// What one registration passed to the innermost RegisterImpl, which is what the id order compares. A typed
    /// component's default value is left out: no order has a reason to read it.
    /// </summary>
    public readonly record struct RegisteredComponent(
        Type? Type, TContext Context, ModComponentInfo ModComponentInfo, string TypeFullName);

    private readonly List<RegisteredComponent> _registered = [];
    private readonly Dictionary<string, byte> _ids = new();

    protected IdComponentRegistryBase(
        IEnumerable<IComponentRegistrationBase<TRegistry, TComponent>> registrations,
        Comparison<RegisteredComponent> idOrder)
        : base(registrations)
    {
        // The base constructor ran every registration and sealed
        _registered.Sort(idOrder);

        for (var id = 0; id < _registered.Count; id++)
        {
            _ids.Add(_registered[id].TypeFullName, (byte)id);
            OnIdAssigned((byte)id, _registered[id]);
        }
    }

    /// <summary>Full name order, ordinal: the default, so the ids do not depend on one machine's registration order.</summary>
    protected IdComponentRegistryBase(IEnumerable<IComponentRegistrationBase<TRegistry, TComponent>> registrations)
        : this(registrations, (left, right) => string.CompareOrdinal(left.TypeFullName, right.TypeFullName))
    {
    }

    /// <summary>Every registered component, in id order: a component's id is its index.</summary>
    protected IReadOnlyList<RegisteredComponent> RegisteredComponents
    {
        get
        {
            ThrowIfNotSealed("its ids");
            return _registered;
        }
    }

    /// <summary>
    /// Runs for every component once ids are handed out, in id order. Called from this constructor, so an override
    /// reads only fields set by initializers.
    /// </summary>
    protected virtual void OnIdAssigned(byte id, RegisteredComponent component)
    {
    }

    protected byte GetId(string typeFullName)
    {
        ThrowIfNotSealed(typeFullName);
        return _ids[typeFullName];
    }

    protected override TRegistry RegisterComponentImpl<T>(TContext context, T defaultValue = default)
    {
        ThrowIfFull();
        var registry = base.RegisterComponentImpl(context, defaultValue);
        _registered.Add(new RegisteredComponent(typeof(T), context, default, typeof(T).FullName!));
        return registry;
    }

    protected override TRegistry RegisterModComponentImpl(TContext context, ModComponentInfo info, string typeFullName)
    {
        ThrowIfFull();
        var registry = base.RegisterModComponentImpl(context, info, typeFullName);
        _registered.Add(new RegisteredComponent(null, context, info, typeFullName));
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

/// <summary>An id registry whose components are registered with no context; ValueTuple stands for none.</summary>
internal abstract class IdComponentRegistryBase<TRegistry, TComponent> : IdComponentRegistryBase<TRegistry, TComponent, ValueTuple>
    where TRegistry : IComponentRegistryBase<TRegistry, TComponent>
{
    protected IdComponentRegistryBase(
        IEnumerable<IComponentRegistrationBase<TRegistry, TComponent>> registrations,
        Comparison<RegisteredComponent> idOrder)
        : base(registrations, idOrder)
    {
    }

    protected IdComponentRegistryBase(IEnumerable<IComponentRegistrationBase<TRegistry, TComponent>> registrations)
        : base(registrations)
    {
    }

    protected TRegistry RegisterComponentImpl<T>(T defaultValue = default)
        where T : struct, TComponent
        => RegisterComponentImpl(default(ValueTuple), defaultValue);

    protected TRegistry RegisterComponentImpl(Type componentType, TComponent? defaultValue = default)
        => RegisterComponentImpl(componentType, default(ValueTuple), defaultValue);

    protected TRegistry RegisterModComponentImpl(ModComponentInfo info, string typeFullName)
        => RegisterModComponentImpl(default(ValueTuple), info, typeFullName);
}
