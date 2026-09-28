using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The event is always sent and run locally, and never played from the ECS.
/// Generates the event's <see cref="IGameEvent"/> methods.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class AlwaysPropagatesToEcsOnlyAttribute : Attribute;
