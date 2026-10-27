using ReadyM.SDK.Attributes;
using ReadyM.SDK.Tests.ExternalMod;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>
/// Includes a mixin and an archetype declared in another assembly, whose components this assembly
/// cannot name.
/// </summary>
[Archetype(Networked = false)]
[Include(typeof(Placement))]
[Include(typeof(Wallet))]
[IncludeArchetype(typeof(Merchant))]
public readonly partial struct Trader
{
    public partial int Haggling { get; set; }
}
