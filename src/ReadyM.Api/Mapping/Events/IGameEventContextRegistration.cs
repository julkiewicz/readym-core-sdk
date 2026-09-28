namespace ReadyM.Api.Mapping.Events;

/// <summary>Contributes game event contexts to the <see cref="GameEventContextRegistry"/>, through DI.</summary>
public interface IGameEventContextRegistration
{
    void Register(GameEventContextRegistry registry);
}
