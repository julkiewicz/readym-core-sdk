using System.ComponentModel;
using Friflo.Engine.ECS;

namespace ReadyM.SDK.Archetypes;

/// <summary>Marks a shape whose component is indexed by a value of this type, so it can be looked up by one.</summary>
public interface IIndexed<TKey>
{
    /// <summary>
    /// Hands the finder the component that holds this shape's index, so a lookup reaches it with no registry. The
    /// generator implements it; it is called on the shape's own struct type, so nothing is boxed.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    bool TryFind<TFinder>(ref TFinder finder, TKey key, out RawEntity entity)
        where TFinder : struct, IIndexFinder<TKey>;
}
