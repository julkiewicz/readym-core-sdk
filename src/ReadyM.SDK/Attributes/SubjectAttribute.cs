namespace ReadyM.SDK.Attributes;

/// <summary>
/// Marks the member holding the entity a network event is about. <see cref="Policy"/> checks it, and
/// <see cref="Scope.Area"/> and <see cref="Scope.Cell"/> take their audience from it.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SubjectAttribute : Attribute;
