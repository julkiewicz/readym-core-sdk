namespace ReadyM.SDK.Attributes;

/// <summary>
/// Declares a struct as a network event: something that happened, which travels to whoever
/// <paramref name="scope"/> names and which any mod may validate, change or stop on the way.
/// Declare it in your mod's Common project, so both halves have it.
/// </summary>
/// <param name="scope">Who receives it.</param>
/// <param name="policy">Who may raise it. The server may always raise.</param>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class NetworkEventAttribute(Scope scope, Policy policy) : Attribute
{
    /// <summary>Who receives it.</summary>
    public Scope Scope { get; } = scope;

    /// <summary>Who may raise it.</summary>
    public Policy Policy { get; } = policy;

    /// <summary>
    /// The type a refusal carries back to the origin: any serializable type, not only an enum. Left unset, the
    /// event cannot be cancelled with a reason and <c>Cancel()</c> takes no argument.
    /// </summary>
    public Type? Reject { get; set; }
}
