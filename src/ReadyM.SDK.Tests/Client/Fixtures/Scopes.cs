using Friflo.Engine.ECS;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>Stands in for a scope component the relay owns, indexed the way AreaScopeComponent is.</summary>
internal struct TestAreaScopeComponent : IIndexedComponent<int>
{
    public int AreaId;

    public int GetIndexedValue() => AreaId;
}

/// <summary>An archetype declaring itself a scope, which is all IScope asks for.</summary>
[Archetype(Networked = false)]
[ExplicitComponent(typeof(TestAreaScopeComponent))]
public readonly partial struct TestArea : IScope
{
    public partial int AreaId { get; set; }
}

[ArchetypeMixin(Replication.None)]
public readonly partial struct Patrol
{
    public partial int Route { get; set; }
}

[Archetype(Networked = false)]
[Include(typeof(Health))]
[Include(typeof(Patrol))]
public readonly partial struct Guard;

[Archetype(Networked = false)]
[Include(typeof(Health))]
public readonly partial struct Critter;
