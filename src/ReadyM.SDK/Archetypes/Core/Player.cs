using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Archetypes.Core;

/// <summary>A scope for one connected player, from connecting until disconnecting.</summary>
[Archetype(Replication = Replication.OwnershipBased)]
[ExplicitComponent(typeof(PlayerScopeComponent))]
public readonly partial struct Player : IScope
{
    /// <summary>Which player this is.</summary>
    public partial PlayerId PlayerId { get; set; }
}