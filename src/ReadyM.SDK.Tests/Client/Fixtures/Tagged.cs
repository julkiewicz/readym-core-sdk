using Friflo.Engine.ECS;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>Stands in for a tag a game's own systems ask about, such as who may delete an entity.</summary>
public readonly struct WatchedTag : ITag;

public readonly struct SecondTag : ITag;

/// <summary>A shape whose entities carry tags from the moment they exist.</summary>
[Archetype(Networked = false)]
[Tag(typeof(WatchedTag))]
[Tag(typeof(SecondTag))]
public readonly partial struct Watched
{
    public partial int Marker { get; set; }
}

/// <summary>The same shape without any, so a test can tell tags apart from the archetype.</summary>
[Archetype(Networked = false)]
public readonly partial struct Unwatched
{
    public partial int Marker { get; set; }
}
