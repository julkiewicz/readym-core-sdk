namespace ReadyM.Api.Mapping.Events;

/// <summary>
/// A game event's policy: three questions the mapped event manager asks the event itself. Generated from a
/// discriminator attribute on the event (<see cref="OwnershipBasedAttribute"/> and the rest), or written by hand.
/// </summary>
public interface IGameEvent
{
    /// <summary>The game did it: send it to the ECS?</summary>
    GameEventNotifyResult CanGameEventNotifyEcs(GameEventContextRegistry contexts);

    /// <summary>The game did it: run it here?</summary>
    GameEventResult CanGameEventRunLocally(GameEventContextRegistry contexts);

    /// <summary>The ECS received it: make the game do it?</summary>
    GameEventResult CanEcsInvokeGameEvent(GameEventContextRegistry contexts);
}
