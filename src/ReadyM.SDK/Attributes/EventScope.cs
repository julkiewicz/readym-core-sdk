namespace ReadyM.SDK.Attributes;

/// <summary>Who receives a network event.</summary>
public enum EventScope
{
    /// <summary>The server only. A damage report, a console command.</summary>
    Server,

    /// <summary>Every player. A world event starting, an announcement.</summary>
    Global,

    /// <summary>One player, the member marked <see cref="TargetAttribute"/>. A private message.</summary>
    Single,

    /// <summary>Everyone in the subject's area, or in the area named by <see cref="AreaAttribute"/>.</summary>
    Area,

    /// <summary>Everyone in the subject's cell, or in the cell named by <see cref="CellAttribute"/>.</summary>
    Cell,
}
