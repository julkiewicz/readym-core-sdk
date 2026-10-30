namespace ReadyM.SDK.Attributes;

/// <summary>
/// Marks the member holding the entity a network event is about. <see cref="Policy.SubjectOwner"/> checks it, and
/// <see cref="EventScope.Area"/> and <see cref="EventScope.Cell"/> take their audience from it when the event names
/// no area or cell of its own.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SubjectAttribute : Attribute;
