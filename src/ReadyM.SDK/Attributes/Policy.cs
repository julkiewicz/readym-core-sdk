namespace ReadyM.SDK.Attributes;

/// <summary>Declares who can raise a network event. The server may always raise, so this applies only to clients.</summary>
public enum Policy
{
    /// <summary>Any client.</summary>
    Anyone,
    
    /// <summary>The client that owns the subject entity.</summary>
    SubjectOwner,

    /// <summary>
    /// The client simulating the cell the event names with <see cref="CellAttribute"/>.
    /// </summary>
    CellMaster,

    /// <summary>
    /// The client simulating the area the event names with <see cref="AreaAttribute"/>.
    /// </summary>
    AreaMaster,

    /// <summary>Only the server.</summary>
    ServerAuthoritative
}
