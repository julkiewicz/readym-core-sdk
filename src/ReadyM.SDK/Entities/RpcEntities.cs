using Microsoft.Extensions.Logging;

namespace ReadyM.SDK.Entities;

/// <summary>The world an end looks an RPC's entity parameters up in, and where it says it skipped a handler.</summary>
internal sealed class RpcEntities(IEntityApi api, ILogger logger)
{
    public IEntityApi Api { get; } = api;

    public ILogger Logger { get; } = logger;
}
