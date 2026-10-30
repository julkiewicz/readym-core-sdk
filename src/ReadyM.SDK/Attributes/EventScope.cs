namespace ReadyM.SDK.Attributes;

/// <summary>Who receives a networked event.</summary>
public enum EventScope
{
    /// <summary>The event is delivered only to the server.</summary>
    /// <example>
    /// Console command, trade request, attack, global time skip.
    /// </example>
    Server,

    /// <summary>The event is delivered to every player on the server.</summary>
    /// <example>
    /// A world event starting, an scheduled announcement.
    /// </example>
    Global,

    /// <summary>The event is delivered to a single player, the member marked <see cref="TargetAttribute"/>.</summary>
    /// <example>
    /// A private message.
    /// </example>
    Single,

    /// <summary>The event is delivered to everyone in the subject's area, or in the area named by <see cref="AreaAttribute"/>.</summary>
    /// <example>
    /// A weather change.
    /// </example>
    Area,

    /// <summary>The event is delivered to everyone who has the subject's cell, or in the cell named by <see cref="CellAttribute"/>, activated.</summary>
    /// <example>
    /// A door opening, a projectile being spawned.
    /// </example>
    Cell,
}
