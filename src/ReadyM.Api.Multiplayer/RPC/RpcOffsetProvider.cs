using System;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.Api.Multiplayer.RPC;

internal sealed class RpcOffsetProvider
{
    // TODO: Use VarUShort for this.
    /// <summary>How many codes we can assign to mods. The rest are reserved for the engine.</summary>
    private const int Capacity = RelayMessageCode.MaxServerRpcEvent - RelayMessageCode.MinServerRpcEvent + 1;

    public byte CurrentOffset { get; private set; }

    /// <exception cref="InvalidOperationException">
    /// The sets loaded together need more codes than the space holds.
    /// </exception>
    public byte GetNextOffset(byte events)
    {
        if (CurrentOffset + events > Capacity)
            throw new InvalidOperationException(
                $"The custom code space holds {Capacity} codes, {CurrentOffset} of them already taken, and "
                + $"this contract set needs {events} more. Load fewer mods, or fewer contracts per mod.");

        var offset = CurrentOffset;
        CurrentOffset += events;

        return offset;
    }
}
