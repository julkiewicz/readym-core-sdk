using System;
using ReadyM.Api.Multiplayer.Protocol.Enums;

namespace ReadyM.Api.Multiplayer.RPC;

internal sealed class RpcOffsetProvider
{
    /// <summary>How many codes the space holds, which every contract set loaded together shares.</summary>
    private const int Capacity =
        RelayMessageCode.MaxServerRpcEvent - RelayMessageCode.MinServerRpcEvent + 1;

    public byte CurrentOffset { get; private set; }

    /// <exception cref="InvalidOperationException">
    /// The sets loaded together need more codes than the space holds. It fails here, at startup, because
    /// the alternative is a code that wraps past the end of the space and lands on another set's.
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
