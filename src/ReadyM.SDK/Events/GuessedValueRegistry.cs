using System.ComponentModel;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// The guessed values of one predictable member, by the entity each belongs to. One registry per member, so a
/// read costs a count while nothing is guessed, which is almost always and includes every read on the server.
/// </summary>
/// <remarks>
/// Game thread only, like the entities it is keyed by. A handle carries the world it came from, so two worlds
/// in one process never read each other's guesses.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GuessedValueRegistry<T>
{
    private readonly Dictionary<EntityHandle, T> _guessed = new();

    /// <summary>The guess standing for this entity, where one is.</summary>
    public bool TryGet(in EntityHandle handle, out T value)
    {
        if (_guessed.Count == 0)
        {
            value = default!;

            return false;
        }

        return _guessed.TryGetValue(handle, out value!);
    }

    /// <summary>
    /// Takes the write as a guess, where one is being made. Nothing reaches the entity, and the value is
    /// dropped when the guess is: the read then falls through to whatever the server says at that moment.
    /// </summary>
    /// <returns>Whether this was a guess, and so whether the caller's own write must not happen.</returns>
    public bool Guessing(in EntityHandle handle, T value)
    {
        if (!NetworkEventGuesses.Taking)
            return false;

        var entity = handle;
        _guessed[entity] = value;
        NetworkEventGuesses.Took(() => _guessed.Remove(entity));

        return true;
    }
}
