namespace ReadyM.SDK.Attributes;

/// <summary>Declares a struct as an archetype mixin.</summary>
/// <param name="replication">
/// Whether its values are sent to the other players when it is on a networked archetype, and who may write them.
/// <see cref="Attributes.Replication.None"/> keeps them on the half that wrote them.
/// </param>
[AttributeUsage(AttributeTargets.Struct)]
public class ArchetypeMixinAttribute(Replication replication) : Attribute
{
    public Replication Replication { get; } = replication;

    /// <summary>How its changes travel, when it replicates.</summary>
    public Delivery Delivery { get; set; } = Delivery.Reliable;
}
