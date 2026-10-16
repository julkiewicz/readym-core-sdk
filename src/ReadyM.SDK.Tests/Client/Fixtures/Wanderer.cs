using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

[ArchetypeMixin(Replication.None)]
public readonly partial struct Stride
{
    public partial float Pace { get; set; }
}

[ArchetypeMixin(Replication.None)]
public readonly partial struct Purse
{
    public partial int Gold { get; set; }
}

[ArchetypeMixin(Replication.None)]
public readonly partial struct Mood
{
    public partial float Spirit { get; set; }
}

/// <summary>Carries a managed field, so a query over it also covers a heap that cannot be pinned.</summary>
[ArchetypeMixin(Replication.None)]
public readonly partial struct Label
{
    public partial string Text { get; set; }
}

/// <summary>Six mixins on one entity, which is as wide as a query goes.</summary>
[Archetype(Networked = false)]
[Include(typeof(Health))]
[Include(typeof(Placement))]
[Include(typeof(Stride))]
[Include(typeof(Purse))]
[Include(typeof(Mood))]
[Include(typeof(Label))]
public readonly partial struct Wanderer;
