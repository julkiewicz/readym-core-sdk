namespace ReadyM.Api.Generators.Archetypes;

/// <summary>
/// The contract a component carries so the runtime's policy directory can find its policy. The
/// declaration lives on the shape; this is how it reaches the component the shape generates.
/// </summary>
internal static class PropagationContracts
{
    private const string Tags = "global::ReadyM.Api.Mapping.Tags.";

    public static string? Of(Replication? replication) => replication switch
    {
        Replication.ToEcsOnly => Tags + "IAlwaysPropagatesToEcsOnly",
        Replication.ToGameOnly => Tags + "IAlwaysPropagatesToGameOnly",
        Replication.Both => Tags + "IAlwaysPropagates",
        Replication.ServerAuthoritative => Tags + "IServerAuthoritative",
        Replication.OwnershipBased => Tags + "IOwnershipBased",
        _ => null
    };
}
