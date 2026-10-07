namespace ReadyM.SDK.Attributes;

/// Makes this mixin able to be replicated over the network, if included in a replicated archetype.
/// Mixins without this attribute will not be replicated, even if the archetype is.
[AttributeUsage(AttributeTargets.Struct)]
public sealed class ReplicatedAttribute(Delivery delivery = Delivery.Reliable) : Attribute
{
    public Delivery Delivery { get; } = delivery;
}
