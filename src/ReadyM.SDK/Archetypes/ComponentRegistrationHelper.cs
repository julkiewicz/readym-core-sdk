using System.ComponentModel;
using Friflo.Engine.ECS;
using IComponent = Friflo.Engine.ECS.IComponent;
using ReadyM.Api.DI;
using ReadyM.Api.ECS.Registry;

namespace ReadyM.SDK.Archetypes;

/// <summary>
/// Registers one component a shape brings on the "all" twin. Public and stateless: generated code in any mod assembly
/// calls it, and the registration interfaces are internal to the SDK.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class ComponentRegistrationHelper
{
    public static void Register<T>(IDependencyContainer services)
        where T : struct, IComponent
        => services.RegisterSingleton<IAllComponentRegistration>(new ComponentRegistration<T>());

    private sealed class ComponentRegistration<T> : IAllComponentRegistration
        where T : struct, IComponent
    {
        public void Register(IAllComponentRegistry registry) => registry.RegisterComponent<T>();
    }
}
