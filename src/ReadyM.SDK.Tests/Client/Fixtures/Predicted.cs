using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>
/// A value the server owns and a client may guess at while it waits for the answer, beside one it may not.
/// The pair is the point: only the marked value gets a guess, and the other keeps the write it always had.
/// </summary>
[ArchetypeMixin(Replication.ServerAuthoritative)]
public readonly partial struct Lockdown
{
    [Predictable]
    public partial bool Active { get; set; }

    public partial int Hour { get; set; }
}

/// <summary>The archetype carrying it, so the values are reached as projections the way a mod reaches them.</summary>
[Archetype(Replication = Replication.ServerAuthoritative)]
[Include(typeof(Lockdown), "Lockdown")]
public readonly partial struct Settlement
{
    public partial int Residents { get; set; }
}
