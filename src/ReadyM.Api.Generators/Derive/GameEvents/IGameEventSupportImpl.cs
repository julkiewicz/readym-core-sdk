namespace ReadyM.Api.Generators.Derive.GameEvents;

/// <summary>Emits one discriminator's answers: an expression for each of the three <c>IGameEvent</c> methods.</summary>
internal interface IGameEventSupportImpl : IDeriveSupportImplBase<GameEventModel>
{
    bool NeedsSubject { get; }

    void EmitCanGameEventNotifyEcsBody(CSharpEmitGameEventContext context);
    void EmitCanGameEventRunLocallyBody(CSharpEmitGameEventContext context);
    void EmitCanEcsInvokeGameEventBody(CSharpEmitGameEventContext context);
}
