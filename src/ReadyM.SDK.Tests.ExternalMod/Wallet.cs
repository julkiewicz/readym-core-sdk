using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.ExternalMod;

/// <summary>A mixin another assembly includes, so its component is never nameable from there.</summary>
[ArchetypeMixin(Replication.None)]
public readonly partial struct Wallet
{
    public partial int Coins { get; set; }
}
