namespace ReadyM.SDK.Attributes;

/// <summary>Who may raise a network event. The server may always raise, whatever this says.</summary>
public enum Policy
{
    /// <summary>Any client, and the server. Every event names a policy, so nothing takes this by default.</summary>
    Anyone,
    
    /// <summary>The client that runs the subject.</summary>
    SubjectOwner,

    /// <summary>
    /// The client simulating the cell the event names with <see cref="CellAttribute"/>. The sender is not named in
    /// the event: it is whoever the server received it from.
    /// </summary>
    CellMaster,

    /// <summary>
    /// The client simulating the area the event names with <see cref="AreaAttribute"/>. The sender is not named in
    /// the event: it is whoever the server received it from.
    /// </summary>
    AreaMaster,

    /// <summary>The server alone.</summary>
    ServerAuthoritative
}
