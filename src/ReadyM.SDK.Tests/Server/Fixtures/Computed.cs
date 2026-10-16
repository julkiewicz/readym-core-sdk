using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Tests.Server.Fixtures;

/// <summary>Works out what it says from its values, which whatever includes it gets too.</summary>
[ArchetypeMixin]
public readonly partial struct Stamina
{
    public partial int Value { get; set; }

    public partial int Max { get; set; }

    public bool IsSpent => Value <= 0;

    public int Missing
    {
        get => Max - Value;
        set => Value = Max - value;
    }
}

[Archetype(replicated: false)]
[Include(typeof(Stamina))]
public readonly partial struct Runner;
