using System.ComponentModel;
using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <summary>
/// The players an accepted network event is sent to. A client only ever sends an event to the server; the server
/// decides the audience from the event's <see cref="Attributes.Scope"/> and sends it on from there, so no event
/// is ever relayed between peers without the server reading it.
/// </summary>
/// <exclude />
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct EventAudience
{
    private EventAudience(Scope scope, PlayerId player, AreaId area, FullCellId cell)
        => (Kind, Player, Area, Cell) = (scope, player, area, cell);

    /// <summary>Which of the audiences below this is.</summary>
    public Scope Kind { get; }

    /// <summary>The one player, for <see cref="Scope.Single"/>.</summary>
    public PlayerId Player { get; }

    /// <summary>The area, for <see cref="Scope.Area"/>.</summary>
    public AreaId Area { get; }

    /// <summary>The cell, for <see cref="Scope.Cell"/>.</summary>
    public FullCellId Cell { get; }

    /// <summary>Nobody. The event stops at the server.</summary>
    public static EventAudience Server { get; } = new(Scope.Server, default, default, default);

    /// <summary>Every connected player.</summary>
    public static EventAudience Global { get; } = new(Scope.Global, default, default, default);

    /// <summary>One player.</summary>
    public static EventAudience Single(PlayerId player) => new(Scope.Single, player, default, default);

    /// <summary>Everyone in an area.</summary>
    public static EventAudience InArea(AreaId area) => new(Scope.Area, default, area, default);

    /// <summary>Everyone in a cell.</summary>
    public static EventAudience InCell(FullCellId cell) => new(Scope.Cell, default, default, cell);
}
