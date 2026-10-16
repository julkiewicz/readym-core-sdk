using LiteNetLib;

namespace ReadyM.Relay.Server.Sdk.Ecs.Networked;

/// <summary>The delivery a v0 mod declares as a byte, in the transport's terms.</summary>
internal static class ModDelivery
{
    /// <summary>
    /// 0 is reliable, which means ordered as well: a mod choosing reliability wants changes applied in the order
    /// it made them, and the SDK offers no unordered option to mean anything else. Anything else is unreliable.
    /// </summary>
    public static DeliveryMethod Of(byte delivery)
        => delivery == 0 ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
}
