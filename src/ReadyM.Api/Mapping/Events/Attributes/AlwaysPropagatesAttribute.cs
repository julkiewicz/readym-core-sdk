using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The event is always sent, always run locally and always played.
/// Generates the event's <see cref="IGameEvent"/> methods.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AlwaysPropagatesAttribute : Attribute;
