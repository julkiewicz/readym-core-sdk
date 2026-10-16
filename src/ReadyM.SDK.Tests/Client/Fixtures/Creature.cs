using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>The archetype Monster includes, which is what gives Monster a conversion up to it.</summary>
[Archetype(Networked = false)]
[Include(typeof(Health))]
public readonly partial struct Creature;
