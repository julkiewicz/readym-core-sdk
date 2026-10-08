using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

[Archetype(replicated: false)]
[Include(typeof(Health))]
[Include(typeof(Placement))]
public readonly partial struct Chest
{
    public partial int Gold { get; set; }
}

/// Everything a chest is, plus loot. Composition in place of an optional include.
[Archetype(replicated: false)]
[IncludeArchetype(typeof(Chest))]
[Include(typeof(Loot))]
public readonly partial struct LootedChest;
