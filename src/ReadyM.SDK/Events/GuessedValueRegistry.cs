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
    private readonly Dictionary<EntityHandle, Held> _guessed = new();

    /// <summary>Reused, so dropping an answered guess allocates nothing on a path the server walks too.</summary>
    private readonly List<EntityHandle> _answered = [];

    private readonly string _member;

    private long _stamped;

    /// <param name="member">The value this holds guesses at, for the message when one cannot be made.</param>
    /// <param name="component">
    /// What the value is stored in, so a delta carrying it says the guess has been answered. The component
    /// rather than the field: the server sends the component, and nothing downstream of that knows which of
    /// its fields moved. Dropping a guess because the field beside it changed is early, never wrong, since
    /// the read then falls through to the server's own value.
    /// </param>
    public GuessedValueRegistry(string member, Type component)
    {
        _member = member;
        GuessedValues.Holds(component, Answered);
    }

    /// <summary>The guess standing for this entity, where one is.</summary>
    public bool TryGet(in EntityHandle handle, out T value)
    {
        if (_guessed.Count == 0)
        {
            value = default!;

            return false;
        }

        if (!_guessed.TryGetValue(handle, out var held))
        {
            value = default!;

            return false;
        }

        value = held.Value;

        return true;
    }

    /// <summary>
    /// Holds the value as a guess. Nothing reaches the entity, and it is dropped when the guess is answered:
    /// the read then falls through to whatever the server says at that moment.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nothing is being guessed at here, so there is no raise waiting on an answer to take this back and it
    /// would stand for the rest of the session.
    /// </exception>
    public void Guess(in EntityHandle handle, T value)
    {
        if (!NetworkedEventGuesses.Taking)
            throw new InvalidOperationException(
                $"'{_member}' can only be guessed at inside the callback handed to .Predicting() on the raise "
                + "being guessed at. Nothing here is waiting on an answer that would take the guess back.");

        var entity = handle;
        var stamp = ++_stamped;

        _guessed[entity] = new Held(value, stamp);
        NetworkedEventGuesses.Took(() => Drop(entity, stamp));
    }

    /// <summary>
    /// Takes back one raise's guess, and only its own. A later raise guessing at the same value on the same
    /// entity replaces it, and the earlier raise's deadline must not then drop what replaced it.
    /// </summary>
    private void Drop(EntityHandle entity, long stamp)
    {
        if (_guessed.TryGetValue(entity, out var held) && held.Stamp == stamp)
            _guessed.Remove(entity);
    }

    /// <summary>
    /// The server's own value has arrived, so every guess at it on this entity is answered, whichever raise
    /// made it. Dropping twice is ordinary: the raise's own deadline comes round later and finds nothing.
    /// </summary>
    private void Answered(int entity)
    {
        if (_guessed.Count == 0)
            return;

        foreach (var held in _guessed.Keys)
            if (held.Id == entity)
                _answered.Add(held);

        foreach (var held in _answered)
            _guessed.Remove(held);

        _answered.Clear();
    }

    private readonly struct Held(T value, long stamp)
    {
        public T Value { get; } = value;

        /// <summary>Which guess this is, so an older raise's deadline cannot drop a newer raise's guess.</summary>
        public long Stamp { get; } = stamp;
    }
}
