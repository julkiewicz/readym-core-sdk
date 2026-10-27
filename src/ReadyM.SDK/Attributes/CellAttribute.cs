namespace ReadyM.SDK.Attributes;

/// <summary>
/// Marks the member naming the cell a <see cref="EventScope.Cell"/> event goes to, instead of taking it from the
/// subject.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CellAttribute : Attribute;
