namespace ReadyM.SDK.Attributes;

/// <summary>
/// Declares a struct as a network event: a message sent to the server or other players,
/// which any mod may validate, change, cancel, or observe.
/// </summary>
/// <remarks>
/// Declare it in your mod's Common project, so both sides can refer to it.
/// </remarks>
/// <param name="scope">Who receives the event.</param>
/// <param name="policy">Which players can raise the event. The server always can.</param>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class NetworkedEventAttribute(EventScope scope, Policy policy) : Attribute
{
    /// <summary>Who receives the event.</summary>
    public EventScope Scope { get; } = scope;

    /// <summary>Who may raise the event.</summary>
    public Policy Policy { get; } = policy;

    /// <summary>
    /// An optional, serializable type, that is sent back to the client when the event is cancelled.
    /// Used to return a reason for the cancellation, or to return a value that the client can use to recover from the cancellation.
    /// </summary>
    public Type? Reject { get; set; }
}
