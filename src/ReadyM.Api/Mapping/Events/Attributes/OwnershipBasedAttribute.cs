using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The event runs where the subject entity is owned: the owner sends it, everyone else plays it.
/// Chooses the policy <see cref="DeriveIGameEventAttribute"/> generates.
/// </summary>
/// <param name="subject">The field holding the subject entity, written with <c>nameof</c>: an <c>Entity</c> or a
/// <c>RawEntity</c>.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class OwnershipBasedAttribute(string subject) : Attribute
{
    public string Subject { get; } = subject;
}
