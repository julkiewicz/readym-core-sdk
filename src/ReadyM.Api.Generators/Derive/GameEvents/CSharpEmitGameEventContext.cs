using System.Text;

namespace ReadyM.Api.Generators.Derive.GameEvents;

/// <summary>What emitted policy code may touch: the result values, the contexts, and the event's subject.</summary>
internal sealed class CSharpEmitGameEventContext(StringBuilder sb, GameEventModel model)
{
    private const string EventsNamespace = "global::ReadyM.Api.Mapping.Events";
    private const string OwnershipContext = "global::ReadyM.Api.Multiplayer.GameEvents.OwnershipContext";
    private const string MasterClientContext = "global::ReadyM.Relay.Client.GameEvents.MasterClientContext";

    public const string ContextsParameter = "contexts";

    public GameEventModel Model { get; } = model;

    public string OwnsSubject
        => $"{ContextsParameter}.GetContext<{OwnershipContext}>().OwnsEntity(this.{Model.Subject!.Name})";

    public string IsMasterClient
        => $"{ContextsParameter}.GetContext<{MasterClientContext}>().IsMasterClient";

    public static string Result(string name)
        => $"{EventsNamespace}.GameEventResult.{name}";

    public static string Notify(string name)
        => $"{EventsNamespace}.GameEventNotifyResult.{name}";

    public void Append(string code)
        => sb.Append(code);
}
