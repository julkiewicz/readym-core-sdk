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
public sealed class GuessedValueRegistry<T>(string member)
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
    /// Holds the value as a guess. Nothing reaches the entity, and it is dropped when the guess is: the read
    /// then falls through to whatever the server says at that moment.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nothing is being guessed at here, so there is no raise waiting on an answer to take this back and it
    /// would stand for the rest of the session.
    /// </exception>
    public void Guess(in EntityHandle handle, T value)
    {
        if (!NetworkEventGuesses.Taking)
            throw new InvalidOperationException(
                $"'{member}' can only be guessed at inside the callback handed to .Predicting() on the raise "
                + "being guessed at. Nothing here is waiting on an answer that would take the guess back.");

        var entity = handle;
        _guessed[entity] = value;
        NetworkEventGuesses.Took(() => _guessed.Remove(entity));
    }
}
