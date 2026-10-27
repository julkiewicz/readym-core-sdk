namespace ReadyM.SDK.Attributes;

/// <summary>
/// Lets a client guess at this value while it waits for the server, inside
/// <c>INetworkedEventRaise.Predicting</c>. The getter reads the guess until it is taken back, so the code that
/// reads the value never learns one was involved.
/// </summary>
/// <remarks>
/// A guess never leaves the machine: it is held beside the entity rather than written into the replicated
/// component, which is why writing a value a client does not own is allowed here and nowhere else. Taking it
/// back DROPS it, so the read falls through to whatever the server says at that moment, never to a default and
/// never to a snapshot from when the guess was made.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PredictableAttribute : Attribute;
