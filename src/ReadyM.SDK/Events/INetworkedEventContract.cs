using System.ComponentModel;
using LiteNetLib.Utils;
using ReadyM.Api.Idents;
using ReadyM.Api.Multiplayer.Protocol.Enums;
using ReadyM.SDK.Attributes;
using ReadyM.SDK.Entities;

namespace ReadyM.SDK.Events;

/// <exclude />
/// <summary>
/// What the Common project generates beside an event struct: everything about the event that its handlers do
/// not decide. There is one per event rather than one per mod, which is what lets the policy be asked once and
/// the event be fanned out once however many mods are handling it.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface INetworkEventContract<T> where T : struct, INetworkEvent
{
    /// <summary>This event's code on the wire, once the host has placed its mod's block.</summary>
    RelayMessageCode Code { get; }

    /// <summary>Who receives it once it is accepted.</summary>
    Scope Scope { get; }

    /// <summary>Who may raise it.</summary>
    Policy Policy { get; }

    /// <summary>The entity it is about, which the policy checks and a scoped audience is read from.</summary>
    EntityHandle? Subject(in T networkEvent);

    /// <summary>The one player a <see cref="Attributes.Scope.Single"/> event goes to.</summary>
    PlayerId Target(in T networkEvent);

    /// <summary>Reads the event back, failing where it names an entity this end does not have.</summary>
    bool TryRead(NetworkEventDispatchCore world, NetDataReader reader, out T networkEvent);

    /// <summary>Writes a refusal's reason, of the type the declaration named, or its absence.</summary>
    void WriteReason(NetDataWriter writer, object? reason);

    /// <summary>Reads that reason back, or null where the refusal carried none.</summary>
    object? ReadReason(NetDataReader reader);
}
