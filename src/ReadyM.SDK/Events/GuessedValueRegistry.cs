using ReadyM.Api.Mapping.Data;
using ReadyM.Api.Multiplayer.ECS;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <summary>
/// The guessed values standing in one world. A guess is written into the component, so the game shows it, and
/// the server's value is kept here until the server answers or the guess is rolled back.
/// </summary>
/// <remarks>
/// A guessed field never goes to the server: every write here clears its dirty bit, and while a guess stands the
/// field is cleared again before each delta is sent. ECS thread only.
/// </remarks>
internal sealed class GuessedValueRegistry(PredictionDeltaHooks deltaHooks)
{
    private readonly Dictionary<(Type Component, int Field), object> _fields = [];

    /// <summary>What rolls back the guesses made so far, while a prediction runs; null otherwise.</summary>
    private List<Action>? _rollbacks;

    /// <summary>
    /// Runs a prediction, collecting how to roll back each guess it makes.
    /// </summary>
    /// <returns>What rolls back every guess the prediction made, or null when it made none.</returns>
    public Action? RunPrediction(Action prediction)
    {
        var outer = _rollbacks;
        var rollbacks = new List<Action>();
        _rollbacks = rollbacks;

        try
        {
            prediction();
        }
        finally
        {
            _rollbacks = outer;
        }

        return rollbacks.Count == 0 ? null : () => rollbacks.ForEach(rollback => rollback());
    }

    /// <summary>
    /// Writes a guess into the component. A guess on an entity that already has one joins it: the server's value
    /// kept is the one from before the first, and the first raise's timeout still applies.
    /// </summary>
    /// <param name="member">The predictable member, as the exception names it.</param>
    /// <exception cref="InvalidOperationException">Called outside a <c>.Predicting()</c> callback.</exception>
    public void Guess<TComponent, T>(EntityHandle entity, Field<TComponent, T> field, T value, string member)
        where TComponent : struct, INetworkedComponent
    {
        if (_rollbacks is not { } rollbacks)
            throw new InvalidOperationException(
                $"'{member}' can only be predicted inside the callback passed to .Predicting().");

        rollbacks.Add(FieldGuesses(field).Guess(entity, value));
    }

    private FieldGuessRegistry<TComponent, T> FieldGuesses<TComponent, T>(Field<TComponent, T> field)
        where TComponent : struct, INetworkedComponent
    {
        var key = (typeof(TComponent), field.Id);

        if (!_fields.TryGetValue(key, out var guesses))
            _fields[key] = guesses = new FieldGuessRegistry<TComponent, T>(field, deltaHooks.For<TComponent>());

        return (FieldGuessRegistry<TComponent, T>)guesses;
    }

    /// <summary>The guesses at one field, by the entity each stands on.</summary>
    private sealed class FieldGuessRegistry<TComponent, T> where TComponent : struct, INetworkedComponent
    {
        private readonly Dictionary<int, GuessedValue> _guesses = [];

        private readonly Field<TComponent, T> _field;

        private readonly PredictionDeltaHooks<TComponent> _deltaHooks;

        private readonly PredictionDeltaHooks<TComponent>.DeltaHook _beforeDeltaRead;

        private readonly PredictionDeltaHooks<TComponent>.DeltaHook _afterDeltaRead;

        private readonly Action _beforeDeltaSend;

        private long _lastGuessGroup;

        public FieldGuessRegistry(Field<TComponent, T> field, PredictionDeltaHooks<TComponent> deltaHooks)
        {
            _field = field;
            _deltaHooks = deltaHooks;
            _beforeDeltaRead = BeforeDeltaRead;
            _afterDeltaRead = AfterDeltaRead;
            _beforeDeltaSend = BeforeDeltaSend;
        }

        /// <returns>What rolls this guess back.</returns>
        public Action Guess(EntityHandle entity, T value)
        {
            ref var component = ref entity.GetComponent<TComponent>();

            var joins = _guesses.TryGetValue(entity.Id, out var standing);
            var serverValue = joins ? standing.ServerValue : _field.Get(component);
            var group = joins ? standing.Group : ++_lastGuessGroup;

            if (_guesses.Count == 0)
                SubscribeToDeltaHooks();

            _guesses[entity.Id] = new GuessedValue
            {
                Entity = entity,
                ServerValue = serverValue,
                Guess = value,
                Group = group,
            };

            WriteWithoutReplicating(ref component, value, entity.Id);

            return () => RestoreServerValue(entity.Id, group);
        }

        /// <summary>Rolls back one group of guesses, unless the server answered it or a newer group replaced it.</summary>
        private void RestoreServerValue(int entityId, long group)
        {
            if (!_guesses.TryGetValue(entityId, out var guessed) || guessed.Group != group)
                return;

            _guesses.Remove(entityId);

            // The entity may be gone, say a creature that died to the hit being guessed at
            if (HasLiveComponent(guessed.Entity))
                WriteWithoutReplicating(ref guessed.Entity.GetComponent<TComponent>(), guessed.ServerValue, entityId);

            if (_guesses.Count == 0)
                UnsubscribeFromDeltaHooks();
        }

        /// <summary>Puts the server's value back, so the delta about to be read applies to it.</summary>
        private void BeforeDeltaRead(int entityId, ref TComponent component)
        {
            if (_guesses.TryGetValue(entityId, out var guessed))
                WriteWithoutReplicating(ref component, guessed.ServerValue, entityId);
        }

        /// <summary>
        /// A delta that changed the field answered the guess, and the new value stands. One that did not leaves the
        /// guess waiting, so it goes back in.
        /// </summary>
        private void AfterDeltaRead(int entityId, ref TComponent component)
        {
            if (!_guesses.TryGetValue(entityId, out var guessed))
                return;

            if (!EqualityComparer<T>.Default.Equals(_field.Get(component), guessed.ServerValue))
            {
                _guesses.Remove(entityId);

                if (_guesses.Count == 0)
                    UnsubscribeFromDeltaHooks();

                return;
            }

            WriteWithoutReplicating(ref component, guessed.Guess, entityId);
        }

        /// <summary>Keeps every guessed field out of the delta about to be sent, whatever marked it changed.</summary>
        private void BeforeDeltaSend()
        {
            foreach (var guessed in _guesses.Values)
                if (HasLiveComponent(guessed.Entity))
                    guessed.Entity.GetComponent<TComponent>().ClearDirty(_field.Id);
        }

        /// <summary>Writes a value the game takes, and that never goes to the server.</summary>
        private void WriteWithoutReplicating(ref TComponent component, T value, int entityId)
        {
            _field.SetFromApi(ref component, value, entityId);
            component.ClearDirty(_field.Id);
        }

        private static bool HasLiveComponent(EntityHandle entity)
            => entity.IsAlive() && entity.HasComponent<TComponent>();

        private void SubscribeToDeltaHooks()
        {
            _deltaHooks.BeforeDeltaRead += _beforeDeltaRead;
            _deltaHooks.AfterDeltaRead += _afterDeltaRead;
            _deltaHooks.BeforeDeltaSend += _beforeDeltaSend;
        }

        private void UnsubscribeFromDeltaHooks()
        {
            _deltaHooks.BeforeDeltaRead -= _beforeDeltaRead;
            _deltaHooks.AfterDeltaRead -= _afterDeltaRead;
            _deltaHooks.BeforeDeltaSend -= _beforeDeltaSend;
        }

        private struct GuessedValue
        {
            public EntityHandle Entity;

            /// <summary>The server's value from before the first guess in the group.</summary>
            public T ServerValue;

            public T Guess;

            /// <summary>Which group of stacked guesses this is, so rolling back an old group misses a newer one.</summary>
            public long Group;
        }
    }
}
