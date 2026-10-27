namespace ReadyM.SDK.Attributes;

/// <summary>Who may raise a network event. The server may always raise, whatever this says.</summary>
public enum Policy
{
    /// <summary>Any client, and the server. Every event names a policy, so nothing takes this by default.</summary>
    Anyone,
    
    /// <summary>The client that runs the subject.</summary>
    SubjectOwner,

    /// <summary>The client simulating the subject's cell.</summary>
    CellMaster,

    /// <summary>The client simulating the subject's area.</summary>
    AreaMaster,

    /// <summary>The server alone.</summary>
    ServerAuthoritative
}
