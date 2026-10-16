using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Examples.Declarations;

[Archetype(Networked = false)]
[Include(typeof(Vitals))]
public readonly partial struct Npc;
