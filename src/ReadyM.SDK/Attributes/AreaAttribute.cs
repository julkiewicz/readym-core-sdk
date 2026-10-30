namespace ReadyM.SDK.Attributes;

/// <summary>
/// Marks the member naming the area a networked event is in, an <c>AreaId</c> or an <c>Area</c>. A
/// <see cref="EventScope.Area"/> event goes to it rather than to the subject's area, and a
/// <see cref="Policy.AreaMaster"/> event is checked against its master.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AreaAttribute : Attribute;
