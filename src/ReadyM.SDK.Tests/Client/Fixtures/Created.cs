using Microsoft.Extensions.Logging;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Client.Fixtures;

/// <summary>What a shape does for itself as one of its entities appears: the place for a default.</summary>
[ArchetypeMixin(Replication.None)]
public readonly partial struct Stamped
{
    public partial int Mark { get; set; }

    [CreateHandler]
    private void OnCreated() => Mark = 7;
}

/// <summary>The same, asking the game for what it needs. A handler's parameters come from its services.</summary>
[ArchetypeMixin(Replication.None)]
public readonly partial struct Announced
{
    public partial int Heard { get; set; }

    [CreateHandler]
    private void OnCreated(ILogger log)
    {
        log.LogDebug("announced");
        Heard = 1;
    }
}

[Archetype(Networked = false)]
[Include(typeof(Stamped))]
public readonly partial struct Parcel;

/// <summary>
/// An archetype's own handler runs as well as the one on what it includes. Which of them goes first
/// is nobody's to say, so this sets a value of its own rather than reading the other's.
/// </summary>
[Archetype(Networked = false)]
[Include(typeof(Stamped))]
public readonly partial struct Crate
{
    public partial int Seal { get; set; }

    [CreateHandler]
    private void OnCreated() => Seal = 1;
}

[Archetype(Networked = false)]
[Include(typeof(Announced))]
public readonly partial struct Herald;

/// <summary>
/// Several mixins each asking for something, under an archetype asking for something of its own.
/// Creating one entity runs all of them, which is what makes a default a mixin's own business.
/// </summary>
[Archetype(Networked = false)]
[Include(typeof(Stamped))]
[Include(typeof(Announced))]
[Include(typeof(Counted))]
public readonly partial struct Convoy
{
    public partial int Sealed { get; set; }

    [CreateHandler]
    private void OnCreated() => Sealed = 4;
}

[ArchetypeMixin(Replication.None)]
public readonly partial struct Counted
{
    public partial int Tally { get; set; }

    [CreateHandler]
    private void OnCreated() => Tally = 3;
}
