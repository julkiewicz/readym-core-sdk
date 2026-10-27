namespace ReadyM.SDK.Attributes;

/// <summary>
/// Marks the member naming the area a <see cref="EventScope.Area"/> event goes to, instead of taking it from the
/// subject.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AreaAttribute : Attribute;
