namespace ReadyM.SDK.Attributes;

/// Saves this shape's values with the entities that carry it, under a save name.
/// <param name="name">
/// How a save names the component. Unique across every mod and the game, and fixed once saves exist: renaming it
/// orphans what was saved under the old name.
/// </param>
/// <param name="version">The schema version stamped on the component's save records, raised when their shape changes.</param>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class PersistedAttribute(string name, ushort version = 1) : Attribute
{
    public string Name { get; } = name;

    public ushort Version { get; } = version;
}
