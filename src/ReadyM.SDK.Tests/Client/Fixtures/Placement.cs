using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

[ArchetypeMixin(Replication.None)]
public readonly partial struct Placement
{
    public partial float X { get; set; }
    public partial float Y { get; set; }
}
