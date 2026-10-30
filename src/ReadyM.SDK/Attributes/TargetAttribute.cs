namespace ReadyM.SDK.Attributes;

/// <summary>Marks the member naming the one player a <see cref="EventScope.Single"/> event goes to.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TargetAttribute : Attribute;
