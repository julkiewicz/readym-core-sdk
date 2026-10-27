using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using ReadyM.Api.Generators.Derive.CSharp;

namespace ReadyM.Api.Generators.Archetypes;

/// <summary>Checks what a shape asks to replicate, before anything is emitted against it.</summary>
internal static class ReplicationRules
{
    public static readonly DiagnosticDescriptor NothingToReplicate = new(
        "READYM009",
        "Shape has nothing to replicate",
        "'{0}' holds no values of its own, so there is nothing for Replication to decide. Use Replication.None "
        + "on a mixin, and leave it off an archetype. Creating the entity already tells the other side it is this "
        + "shape.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CollectionIsExposed = new(
        "READYM011",
        "Replicated collection is reachable as a whole",
        "'{0}.{1}' is a collection on a replicated shape, so it has to be declared private. Handing "
        + "the collection itself to a caller lets them change it without the change being sent. Its "
        + "own members, Add{1} and the rest, are generated public.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReplicationUnstated = new(
        "READYM014",
        "Networked archetype does not say how its own values replicate",
        "'{0}' is networked and declares values of its own, so it has to say whether they are sent and who may "
        + "write them: add Replication = ... to [Archetype], Replication.None to keep them local. Left unsaid, "
        + "nothing stops a client writing a value it does not own.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReplicationDisagrees = new(
        "READYM015",
        "Replication disagrees with the component behind the shape",
        "'{0}' says Replication.{1}, but '{2}' backs it and is Replication.{3}. The component decides, so say "
        + "Replication.{3}.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ReplicationOnLocalArchetype = new(
        "READYM016",
        "Replication is stated on an archetype that is not networked",
        "'{0}' is not networked, so nothing of it is sent. Drop Replication, or make it networked.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CollectionNeedsReplication = new(
        "READYM018",
        "Collection sits on a shape nothing sends",
        "'{0}.{1}' is a collection, and only a replicated component is given the members that work "
        + "one: Add{1}, Clear{1} and the rest. On '{0}' it would be a handle to memory with nothing "
        + "to reach it by. Replicate the shape, or hold the values some other way.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ValueCannotBeSent = new(
        "READYM046",
        "Replicated value cannot be sent",
        "'{0}.{1}' is a '{2}', which the SDK cannot send, so '{0}' would fail when it is replicated. "
        + "Use a number, bool, enum, vector, NativeString, NativeList, NativeDictionary or a type with "
        + "Serialize and Deserialize methods, or keep the value on a shape that does not replicate.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DeliveryWithoutReplication = new(
        "READYM050",
        "Delivery is stated on a shape that sends nothing of its own",
        "'{0}' sends no values of its own, so it has no Delivery to choose. Drop Delivery.",
        "ReadyM",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>What a shape states about replicating, and whether it is in a position to state it.</summary>
    public static ImmutableArray<Diagnostic> CheckStated(DeclarationModel model)
    {
        var found = new List<Diagnostic>();
        var at = model.Symbol.Locations.FirstOrDefault() ?? Location.None;
        var stated = model.Replication;
        var holdsValues = model.Accessors.Count > 0;

        if (model.IsArchetype && model.Networked == false)
        {
            if (stated is not null)
                found.Add(Diagnostic.Create(ReplicationOnLocalArchetype, at, model.Name));
        }
        else if (model.IsArchetype && holdsValues && stated is null)
        {
            found.Add(Diagnostic.Create(ReplicationUnstated, at, model.Name));
        }
        else if (model.ExplicitComponent is { } component)
        {
            // A shape over a component it does not own says what that component does, and is held to it
            var actual = DeclarationModel.ReplicationOf(component);

            if (stated is { } said && said != actual)
                found.Add(Diagnostic.Create(ReplicationDisagrees, at, model.Name, said, component.ToDisplayString(), actual));
        }
        else if (!holdsValues && stated is { } said && (model.IsArchetype || said != Replication.None))
        {
            found.Add(Diagnostic.Create(NothingToReplicate, at, model.Name));
        }

        if (model.StatesDelivery && !(model.ExplicitComponent is null && model.IsReplicated))
            found.Add(Diagnostic.Create(DeliveryWithoutReplication, at, model.Name));

        return [.. found];
    }

    public static ImmutableArray<Diagnostic> Check(DeclarationModel model)
    {
        var found = new List<Diagnostic>();
        var at = model.Symbol.Locations.FirstOrDefault() ?? Location.None;

        if (model.ExplicitComponent is not null)
            return [];

        // The collection rules hold whether or not the shape asked to replicate, because what a collection
        // is reachable by, and what nothing is reachable by, is decided before anything is sent.
        foreach (var accessor in model.Accessors)
        {
            var declared = accessor.Declared?.Locations.FirstOrDefault() ?? at;

            if (accessor.IsNativeContainer)
            {
                if (!model.IsReplicated)
                    found.Add(Diagnostic.Create(CollectionNeedsReplication, declared, model.Name, accessor.Name));
                else if (accessor.IsExposed)
                    found.Add(Diagnostic.Create(CollectionIsExposed, declared, model.Name, accessor.Name));
            }

            if (model.IsReplicated && accessor.Declared is { } property && !CSharpFieldSupportRegistry.CanSend(property.Type))
                found.Add(Diagnostic.Create(ValueCannotBeSent, declared, model.Name, accessor.Name,
                    property.Type.ToDisplayString()));
        }

        return [.. found];
    }
}
