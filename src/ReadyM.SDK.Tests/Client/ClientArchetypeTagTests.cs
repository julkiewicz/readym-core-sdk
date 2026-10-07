using Friflo.Engine.ECS;
using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Entities;
using ReadyM.SDK.Tests.Client.Fixtures;

namespace ReadyM.SDK.Tests.Client;

/// <summary>
/// The tags a shape declares, carried by every entity created as it.
/// </summary>
/// <remarks>
/// The client holds the world in process, so the tag type a mod named is the one the schema was built
/// from and no name round trip is needed. The archetype is what carries a tag in Friflo, so this is
/// settled when the set is first resolved rather than per entity.
/// </remarks>
public class ClientArchetypeTagTests : ClientSdkTest
{
    [Fact]
    public void An_entity_carries_the_tags_its_shape_declared()
    {
        var watched = Entities.Create<Watched>();

        Assert.True(Entity(watched).Tags.Has<WatchedTag>());
        Assert.True(Entity(watched).Tags.Has<SecondTag>());
    }

    [Fact]
    public void A_shape_with_no_tags_carries_none()
    {
        var plain = Entities.Create<Unwatched>();

        Assert.False(Entity(plain).Tags.Has<WatchedTag>());
        Assert.Equal(0, Entity(plain).Tags.Count);
    }

    /// <summary>Every entity of the shape, not only the first that resolved the archetype.</summary>
    [Fact]
    public void Each_entity_of_the_shape_carries_them()
    {
        Entities.Create<Watched>();

        var second = Entities.Create<Watched>();

        Assert.True(Entity(second).Tags.Has<WatchedTag>());
    }

    /// Tags do not widen the shape: a query for it still finds what it always did.
    [Fact]
    public void The_shape_is_still_found_by_its_components()
    {
        Entities.Create<Watched>().Marker = 7;

        var markers = 0;

        foreach (var found in Entities.Query<Watched>())
            markers += found.Marker;

        Assert.Equal(7, markers);
    }

    private Entity Entity<T>(in T shape) where T : struct, IEntityShape
        => Store.GetEntityByRawEntity(shape.Handle.RawEntity);
}
