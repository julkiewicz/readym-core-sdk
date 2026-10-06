namespace ReadyM.SDK.Attributes;

/// Puts a tag on every entity created as this archetype.
/// <param name="tag">
/// The tag type, which has to be a struct implementing Friflo's <c>ITag</c>. Named by
/// <c>typeof</c> because a tag carries no values and so has nothing to declare a member from.
/// </param>
[AttributeUsage(AttributeTargets.Struct, AllowMultiple = true)]
public sealed class TagAttribute(Type tag) : Attribute
{
    public Type Tag { get; } = tag;
}
