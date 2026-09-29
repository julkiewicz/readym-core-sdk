using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The event is always sent and run locally, and never played from the ECS.
/// Chooses the policy <see cref="DeriveIGameEventAttribute"/> generates.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AlwaysPropagatesToEcsOnlyAttribute : Attribute;
