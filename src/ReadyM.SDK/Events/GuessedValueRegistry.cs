using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReadyM.Api.Mapping.Data;
using ReadyM.Api.Multiplayer.ECS;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// The guesses at one predictable member, by the entity each stands on. A guess is written into the component
/// itself, because the component is what the game is told about, and the server's value is kept here until
/// the guess is answered or taken back.
/// </summary>
/// <remarks>
/// <para>
/// A guess must never reach the server as the value, so every write here leaves the field out of the next
/// delta, and while a guess stands the field is held out of what is sent whatever else marks it changed. Both
/// writes go in as changes from the API, which is the mark that has the game take a value from the ECS.
/// </para>
/// <para>
/// The delta path is listened to only while a guess stands, so with none it pays a null check. Game thread
/// only, like the entities it is keyed by. A handle carries the world it came from, so two worlds in one
/// process never answer each other's guesses.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class GuessedValueRegistry<TComponent, T>
    where TComponent : struct, INetworkedComponent
{
    private readonly Dictionary<EntityHandle, Held> _guessed = new();

    /// <summary>Reused, so a delta answering guesses allocates nothing.</summary>
    private readonly List<EntityHandle> _answered = [];

    private readonly string _member;

    private readonly Field<TComponent, T> _field;

    private readonly PredictionDeltaHooks<TComponent>.DeltaHook _landing;

    private readonly PredictionDeltaHooks<TComponent>.DeltaHook _landed;

    private readonly Action _sending;

    private long _stamped;

    /// <param name="member">The value this holds guesses at, for the message when one cannot be made.</param>
    /// <param name="field">Where the value lives, which is where a guess is written and the server's value put back.</param>
    public GuessedValueRegistry(string member, Field<TComponent, T> field)
    {
        _member = member;
        _field = field;
        _landing = Landing;
        _landed = Landed;
        _sending = Sending;
    }

    /// <summary>
    /// Writes the value into the component as a guess, keeping the server's value to put back. A guess on top
    /// of a guess joins it: the value kept is the one from before the first, since that one is the server's,
    /// and the stack keeps the first raise's deadline rather than taking the newest one's. A stream of raises,
    /// damage over time say, would otherwise keep pushing the deadline out and never let the guess go.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nothing is being guessed at here, so there is no raise waiting on an answer to take this back and it
    /// would stand for the rest of the session.
    /// </exception>
    public void Guess(in EntityHandle handle, T value)
    {
        if (!NetworkedEventEcsValueGuesses.InsidePrediction)
            throw new InvalidOperationException(
                $"'{_member}' can only be guessed at inside the callback handed to .Predicting() on the raise "
                + "being guessed at. Nothing here is waiting on an answer that would take the guess back.");

        var entity = handle;
        ref var component = ref entity.GetComponent<TComponent>();
        var joins = _guessed.TryGetValue(entity, out var standing);
        var kept = joins ? standing.Kept : _field.Get(component);
        var stack = joins ? standing.Stack : ++_stamped;

        if (_guessed.Count == 0)
            Listen();

        _guessed[entity] = new Held(kept, value, stack);
        Show(ref component, value, entity);
        NetworkedEventEcsValueGuesses.PushRollback(() => TakeBack(entity, stack));
    }

    /// <summary>
    /// Puts the server's value back for one stack of guesses, and only that one. A stack an answer already
    /// ended may have been followed by a new one on the same entity, and a raise from the old stack running
    /// out later must not take back the new one.
    /// </summary>
    private void TakeBack(EntityHandle entity, long stack)
    {
        if (!_guessed.TryGetValue(entity, out var held) || held.Stack != stack)
            return;

        _guessed.Remove(entity);

        // Gone is ordinary, say a creature dying to the hit being guessed at, and leaves nothing to put back
        if (Present(entity))
            Show(ref entity.GetComponent<TComponent>(), held.Kept, entity);

        if (_guessed.Count == 0)
            Quiet();
    }

    /// <summary>The server's value back in place, so the delta about to be read lands on what the server has.</summary>
    private void Landing(ref TComponent component)
    {
        // Walked as pairs: netstandard2.0 has no deconstruction for them
        foreach (var guessed in _guessed)
            if (Is(guessed.Key, ref component))
                Show(ref component, guessed.Value.Kept, guessed.Key);
    }

    /// <summary>
    /// The delta has been read. A field it moved off the server's value was answered, and keeps what arrived;
    /// one it did not carry is still waiting, and gets its guess back. A delta carries only what changed, so
    /// the field beside a guess moving says nothing about the guess.
    /// </summary>
    private void Landed(ref TComponent component)
    {
        foreach (var guessed in _guessed)
        {
            if (!Is(guessed.Key, ref component))
                continue;

            if (EqualityComparer<T>.Default.Equals(_field.Get(component), guessed.Value.Kept))
                Show(ref component, guessed.Value.Guess, guessed.Key);
            else
                _answered.Add(guessed.Key);
        }

        foreach (var entity in _answered)
            _guessed.Remove(entity);

        _answered.Clear();

        if (_guessed.Count == 0)
            Quiet();
    }

    /// <summary>
    /// Keeps every guessed field out of what is about to be sent. The game copies the value it was shown back
    /// into the component, and anything else may mark it changed, and none of that is the server's to hear.
    /// </summary>
    private void Sending()
    {
        foreach (var entity in _guessed.Keys)
            if (Present(entity))
                entity.GetComponent<TComponent>().ClearDirty(_field.Id);
    }

    /// <summary>
    /// Writes a value for the game to show and the server never to hear: as a change from the API, which is
    /// what has the game take it, and then out of the next delta.
    /// </summary>
    private void Show(ref TComponent component, T value, in EntityHandle entity)
    {
        _field.SetFromApi(ref component, value, entity.Id);
        component.ClearDirty(_field.Id);
    }

    /// <summary>
    /// Whether the component the delta path handed over is this entity's, in this entity's world. The id
    /// alone is not enough: a server and its clients share a process in tests and in a listen server.
    /// </summary>
    private static bool Is(in EntityHandle entity, ref TComponent component)
        => Present(entity) && Unsafe.AreSame(ref entity.GetComponent<TComponent>(), ref component);

    /// <summary>Whether the entity is still there with the value on it. Asking a deleted one for a component throws.</summary>
    private static bool Present(in EntityHandle entity)
        => entity.IsAlive() && entity.HasComponent<TComponent>();

    private void Listen()
    {
        PredictionDeltaHooks<TComponent>.BeforeDeltaRead += _landing;
        PredictionDeltaHooks<TComponent>.AfterDeltaRead += _landed;
        PredictionDeltaHooks<TComponent>.BeforeDeltaSend += _sending;
    }

    private void Quiet()
    {
        PredictionDeltaHooks<TComponent>.BeforeDeltaRead -= _landing;
        PredictionDeltaHooks<TComponent>.AfterDeltaRead -= _landed;
        PredictionDeltaHooks<TComponent>.BeforeDeltaSend -= _sending;
    }

    private readonly struct Held(T kept, T guess, long stack)
    {
        /// <summary>The server's value, from before any guess stood on the entity.</summary>
        public T Kept { get; } = kept;

        public T Guess { get; } = guess;

        /// <summary>Which stack this is, so a raise from an ended stack cannot take back a later one.</summary>
        public long Stack { get; } = stack;
    }
}
