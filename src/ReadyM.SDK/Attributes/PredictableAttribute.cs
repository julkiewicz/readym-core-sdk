namespace ReadyM.SDK.Attributes;

/// <summary>
/// Lets a client predict this value locally while it waits for the server confirmation of a networked event.
/// The getter reads the guess until it is confirmed by the server or rolled back.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class PredictableAttribute : Attribute;
