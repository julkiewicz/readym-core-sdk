using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The event is always sent, always run locally and always played.
/// Chooses the policy <see cref="DeriveIGameEventAttribute"/> generates.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AlwaysPropagatesAttribute : Attribute;
