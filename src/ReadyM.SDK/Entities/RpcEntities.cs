using Microsoft.Extensions.Logging;

namespace ReadyM.SDK.Entities;

/// <summary>The world to look an RPC's entity parameters up in.</summary>
internal sealed class RpcEntities(IEntityApi api, ILogger logger)
{
    public IEntityApi Api { get; } = api;

    public ILogger Logger { get; } = logger;
}
