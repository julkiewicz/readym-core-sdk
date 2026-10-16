using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Examples.Declarations;

[Archetype(Networked = false)]
[IncludeArchetype(typeof(ReadyObject))] // include another archetype: its mixins, tags, and a conversion
[Include(typeof(Vitals))] // include a mixin: its accessors appear directly on MainCharacter
// [Include(typeof(Transform), Prefix: "Spawn")]     // prefixed include: SpawnPosition, SpawnRotation
public readonly partial struct MainCharacter
{
    public partial PlayerId PlayerId { get; } // accessor declared directly: goes into the default component
}

/// <summary>A main character that carries equipment. What an optional include used to say.</summary>
[Archetype(Networked = false)]
[IncludeArchetype(typeof(MainCharacter))]
[Include(typeof(Equipment))]
public readonly partial struct EquippedCharacter;
