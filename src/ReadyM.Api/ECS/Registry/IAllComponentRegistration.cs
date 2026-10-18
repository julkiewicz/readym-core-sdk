using Friflo.Engine.ECS;

namespace ReadyM.Api.ECS.Registry;

/// <summary>
/// Contributes component types to the schema: one for the shared components, one per game, one for mods. A type
/// belongs to exactly one of them; registering it twice throws.
/// </summary>
internal interface IAllComponentRegistration : IComponentRegistrationBase<IAllComponentRegistry, IComponent>
{
    // empty
}
