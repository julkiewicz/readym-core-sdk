using Friflo.Engine.ECS;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>Stands in for a component a game core already owns: PascalCase members, one of them readonly.</summary>
internal struct CoreMetadataComponent : IComponent
{
    public readonly int NetId;
    public int Owner;
    public string Tag;

    public CoreMetadataComponent(int netId, int owner, string tag)
    {
        NetId = netId;
        Owner = owner;
        Tag = tag;
    }
}

/// <summary>The member a shape names does not have to match the field's name.</summary>
internal struct CoreScopeComponent : IComponent
{
    public int AreaIdentifier;
}

[ArchetypeMixin(Replication.None)]
[ExplicitComponent(typeof(CoreMetadataComponent))]
public readonly partial struct Metadata
{
    public partial int NetId { get; }
    public partial int Owner { get; set; }
    public partial string Tag { get; set; }
}

[ArchetypeMixin(Replication.None)]
[ExplicitComponent(typeof(CoreScopeComponent))]
public readonly partial struct Scope
{
    [ExplicitMember("AreaIdentifier")]
    public partial int Area { get; set; }
}

/// <summary>An archetype over storage it does not own, so it carries no marker.</summary>
[Archetype(Networked = false)]
[Include(typeof(Metadata))]
[Include(typeof(Scope))]
public readonly partial struct NetworkedThing;

/// <summary>Mixes core-owned storage with a component of its own.</summary>
[Archetype(Networked = false)]
[Include(typeof(Metadata))]
public readonly partial struct Tracked
{
    public partial int Ticks { get; set; }
}
