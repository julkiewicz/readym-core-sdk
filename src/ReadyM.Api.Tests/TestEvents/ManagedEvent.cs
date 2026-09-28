using ReadyM.Api.Mapping.Events;

namespace ReadyM.Api.Tests.TestEvents;

[AlwaysPropagates]
public partial struct ManagedEvent
{
    public int IntValue { get; init; }
    public float FloatValue { get; init; }
}