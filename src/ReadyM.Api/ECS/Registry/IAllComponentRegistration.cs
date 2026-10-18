using Friflo.Engine.ECS;

namespace ReadyM.Api.ECS.Registry;

/// <summary>
/// Contributes component types to the schema. One implementation per source: the default registration for
/// components every game shares, one per game for that game's own, and one for what mods declare.
/// <para>
/// Register a type in exactly one of them. Registration does not deduplicate, so a type registered twice gets
/// two component ids out of a byte-wide space and a second change component. `AreaScopeComponent` belongs to
/// the default registration, not to Wukong's and Oblivion's as well.
/// </para>
/// </summary>
internal interface IAllComponentRegistration : IComponentRegistrationBase<IAllComponentRegistry, IComponent>
{
    // empty
}
