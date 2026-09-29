using System;

namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// The owner of the subject sends the event and only the master client runs it.
/// Chooses the policy <see cref="DeriveIGameEventAttribute"/> generates.
/// </summary>
/// <param name="subject">The field holding the subject entity, written with <c>nameof</c>: an <c>Entity</c> or a
/// <c>RawEntity</c>.</param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RunOnMasterClientOnlyAttribute(string subject) : Attribute
{
    public string Subject { get; } = subject;
}
