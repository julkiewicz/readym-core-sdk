using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>
/// An archetype whose components are a subset of Monster's and Chest's, which is what makes the
/// difference between "is this archetype" and "has these components" observable.
/// </summary>
[Archetype(Networked = false)]
[Include(typeof(Placement))]
public readonly partial struct Prop;
