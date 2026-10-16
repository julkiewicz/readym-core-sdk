using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Archetypes.Core;

/// <summary>A scope for one loaded cell of an area, holding what is in it.</summary>
[Archetype(Replication = Replication.OwnershipBased)]
[ExplicitComponent(typeof(CellScopeComponent))]
public readonly partial struct Cell : IScope
{
    /// <summary>Which cell this is, with its area.</summary>
    [Index]
    public partial FullCellId FullCellId { get; set; }

    /// <summary>
    /// The player whose game simulates what the cell holds, or <c>PlayerId.Invalid</c> while nobody is there.
    /// </summary>
    public partial PlayerId MasterClient { get; set; }
}
