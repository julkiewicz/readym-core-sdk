using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Archetypes.Access;

namespace ReadyM.SDK.Client.Mapping;

/// <summary>The mappings between one shape and one kind of game object.</summary>
public interface IShapeMappingScope<TShape, out TContext>
    where TShape : struct, IArchetypeQueryable
{
    /// <summary>
    /// Declare a mapping between a single shape field and a single context object. Leaving out the
    /// push says the game owns the value and will not be told: a shape declaring
    /// <c>Replication.ToEcsOnly</c> has nothing to push.
    /// </summary>
    IShapeMappingScope<TShape, TContext> Map<TValue>(
        Value<TShape, TValue> value,
        Pull<TValue, TContext> pull,
        Action<TValue, TContext>? push = null);

    /// <summary>
    /// Declare a mapping for one collection. The handler is handed the collection as its component
    /// exposes it rather than the collection itself, so what it changes is what the entity holds.
    /// </summary>
    IShapeMappingScope<TShape, TContext> Map<TAccess>(
        Collection<TShape, TAccess> collection,
        Collected<TAccess, TContext> pull,
        Collected<TAccess, TContext>? push = null)
#if NET
        where TAccess : allows ref struct
#endif
        ;

    /// <summary>
    /// Declare a mapping between the entire shape and a single context object.
    /// Used for mappings that set multiple fields at once.
    /// </summary>
    IShapeMappingScope<TShape, TContext> Map(
        Shape<TShape> shape,
        Action<TShape, TContext> pull,
        Action<TShape, TContext>? push = null);
}
