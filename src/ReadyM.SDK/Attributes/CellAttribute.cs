namespace ReadyM.SDK.Attributes;

/// <summary>
/// Marks the member naming the cell an event is in, a <c>FullCellId</c> or a <c>Cell</c>. A
/// <see cref="EventScope.Cell"/> event goes to it rather than to the subject's cell, and a
/// <see cref="Policy.CellMaster"/> event is checked against its master.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class CellAttribute : Attribute;
