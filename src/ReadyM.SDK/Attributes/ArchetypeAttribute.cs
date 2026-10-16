namespace ReadyM.SDK.Attributes;

/// <summary>Declares a struct as an archetype.</summary>
[AttributeUsage(AttributeTargets.Struct)]
public class ArchetypeAttribute : Attribute
{
    /// <summary>
    /// Whether its entities are given a network identity and an owner, and are sent to the other players. Which of
    /// their values travel with them is up to the mixins, and to <see cref="Replication"/> for the archetype's own.
    /// </summary>
    public bool Networked { get; set; } = true;

    /// <summary>
    /// Whether the archetype's own values are sent, and who may write them. Required on a networked archetype that
    /// declares values of its own, and only there.
    /// </summary>
    public Replication Replication { get; set; }

    /// <summary>How the archetype's own values travel, when it replicates them.</summary>
    public Delivery Delivery { get; set; } = Delivery.Reliable;
}
