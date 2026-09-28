using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The master client sends and runs the event; everyone else plays it.
/// Generates the event's <see cref="IGameEvent"/> methods.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class MasterClientManagedAttribute : Attribute;
