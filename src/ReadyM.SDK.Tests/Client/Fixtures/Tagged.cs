using Friflo.Engine.ECS;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// Stands in for a tag a game's own systems ask about, such as who may delete an entity.
public readonly struct WatchedTag : ITag;

public readonly struct SecondTag : ITag;

/// A shape whose entities carry tags from the moment they exist.
[Archetype(replicated: false)]
[Tag(typeof(WatchedTag))]
[Tag(typeof(SecondTag))]
public readonly partial struct Watched
{
    public partial int Marker { get; set; }
}

/// The same shape without any, so a test can tell tags apart from the archetype.
[Archetype(replicated: false)]
public readonly partial struct Unwatched
{
    public partial int Marker { get; set; }
}
