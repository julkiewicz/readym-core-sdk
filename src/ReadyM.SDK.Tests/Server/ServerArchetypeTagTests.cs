using ReadyM.Api.Idents;
﻿using ReadyM.SDK.Archetypes;
using ReadyM.SDK.Tests.Server.Fixtures;

namespace ReadyM.SDK.Tests.Server;

/// <summary>
/// The tags a shape declares, put on its archetype so every entity of it carries them.
/// </summary>
/// <remarks>
/// A tag is how the relay's own systems ask a yes or no question of an entity: whether its owner may
/// change on entering a scope, whether its scope stays alive while empty. The game used to answer for
/// a mod by hardcoding the tags per archetype, which left a mod unable to say anything about its own.
/// <para>
/// Named across the boundary rather than numbered, because a tag has no id space of its own and the
/// names are all a mod can offer while the schema is still being built.
/// </para>
/// </remarks>
public class ServerArchetypeTagTests : ServerSdkTest
{
    private const string Watched = "ReadyM.SDK.Tests.Server.Fixtures.TestWatchedTag";
    private const string Second = "ReadyM.SDK.Tests.Server.Fixtures.TestSecondTag";

    [Fact]
    public void A_shape_puts_its_tags_on_its_archetype()
    {
        Entities.Create<Watched>();

        Assert.Equal([Watched, Second], Relay.TagsOn(ArchetypeOf<Watched>()));
    }

    /// <summary>Once, however many entities of the shape are created.</summary>
    /// <remarks>
    /// The archetype carries a tag in Friflo, so this belongs to resolving the archetype rather than
    /// to creating an entity. Saying it again per entity would be work on the hottest path there is.
    /// </remarks>
    [Fact]
    public void The_tags_are_put_on_once_however_many_entities_follow()
    {
        Entities.Create<Watched>();
        Entities.Create<Watched>();
        Entities.Create<Watched>();

        Assert.Equal([Watched, Second], Relay.TagsOn(ArchetypeOf<Watched>()));
    }

    [Fact]
    public void A_shape_with_no_tags_puts_none_on()
    {
        Entities.Create<Guard>();

        Assert.Empty(Relay.TagsOn(ArchetypeOf<Guard>()));
    }

    /// <summary>
    /// A tag this game's ECS does not know is refused rather than dropped.
    /// </summary>
    /// <remarks>
    /// Silence would mean the entity quietly misses whatever the tag was for, and a tag is only ever
    /// asked about by a system that would then not run. The mod names it, so the mod can be told.
    /// </remarks>
    [Fact]
    public void A_tag_the_game_does_not_know_is_refused()
    {
        Relay.UnknownTags.Add(Watched);

        var refused = Assert.Throws<InvalidOperationException>(() => Entities.Create<Watched>());

        Assert.Contains(Watched, refused.Message);
    }

    /// Interned by component set, so this is the id the create already used rather than a new one.
    private ArchetypeId ArchetypeOf<T>() where T : struct, IArchetypeQueryable
        => Relay.RegisterArchetype(ComponentsOf<T>().Types);
}
