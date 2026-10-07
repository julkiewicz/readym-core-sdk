using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

[Archetype(replicated: false)]
[IncludeArchetype(typeof(Creature))]
[Include(typeof(Health))]
[Include(typeof(Placement))]
public readonly partial struct Monster
{
    public partial int Level { get; set; }
}
