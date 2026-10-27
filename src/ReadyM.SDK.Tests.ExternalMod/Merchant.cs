using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.ExternalMod;

/// <summary>An archetype another assembly includes, with an accessor of its own and a mixin of its own.</summary>
[Archetype(Networked = false)]
[Include(typeof(Wallet))]
public readonly partial struct Merchant
{
    public partial int Reputation { get; set; }
}
