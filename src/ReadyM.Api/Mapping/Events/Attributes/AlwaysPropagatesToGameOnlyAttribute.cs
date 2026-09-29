using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The event is never sent from here; it only arrives, and is always played.
/// Chooses the policy <see cref="DeriveIGameEventAttribute"/> generates.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AlwaysPropagatesToGameOnlyAttribute : Attribute;
