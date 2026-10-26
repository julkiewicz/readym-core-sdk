namespace ReadyM.SDK.Attributes;

/// <summary>
/// Declares a method as a network event handler. The parameter type says which event, the
/// <paramref name="stage"/> says when it runs. The project it is compiled into says which machine runs it.
/// </summary>
/// <param name="stage">When it runs.</param>
[AttributeUsage(AttributeTargets.Method)]
public sealed class EventHandlerAttribute(Stage stage) : Attribute
{
    /// <summary>When it runs.</summary>
    public Stage Stage { get; } = stage;

    /// <summary>
    /// Skips this handler on the machine that raised the event, which already did the thing itself. Meaningful on
    /// <see cref="Attributes.Stage.Apply"/>, where it is what stops the effect happening twice. The raiser is the
    /// machine that called <c>Raise</c>, which for a server raise on a player's behalf is the server.
    /// </summary>
    public bool SkipOnRaiser { get; set; }

    /// <summary>
    /// The manifest id of a mod this handler runs after, for the rare case where dependency order is not enough.
    /// </summary>
    public string? After { get; set; }
}
