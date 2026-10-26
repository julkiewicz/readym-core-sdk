using ReadyM.Api.Idents;
using ReadyM.SDK.Attributes;

namespace ReadyM.SDK.Events;

/// <summary>
/// The authority check generated dispatch runs before any handler. A client that raises what its policy forbids
/// has the event dropped here, which is where spoofing stops.
/// </summary>
public static class NetworkEventPolicy
{
    /// <summary>Whether <paramref name="origin"/> was allowed to raise an event about <paramref name="subject"/>.</summary>
    public static bool Allows<TShape>(object host, Policy policy, TShape subject, PlayerId origin)
        => throw new NotImplementedException();

    /// <summary>Whether an event with no subject may be raised by <paramref name="origin"/>.</summary>
    public static bool Allows(object host, Policy policy, PlayerId origin) => throw new NotImplementedException();
}
