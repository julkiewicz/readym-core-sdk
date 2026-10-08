namespace ReadyM.SDK.Attributes;

/// Declares a struct as an archetype.
/// <param name="replicated">
/// A replicated entity is given a network identity and an owner, and is replicated to other players.
/// </param>
[AttributeUsage(AttributeTargets.Struct)]
public class ArchetypeAttribute(bool replicated) : Attribute
{
    public bool Replicated { get; } = replicated;

    /// How the archetype's own values travel, when it replicates and holds any.
    public Delivery Delivery { get; set; } = Delivery.Reliable;
}
