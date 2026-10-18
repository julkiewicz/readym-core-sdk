using Friflo.Engine.ECS;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;

namespace ReadyM.Api.Multiplayer.ECS.Registry;

internal class ChangeComponentRegistration(ServerSideSettings serverSide) : IAllComponentRegistration
{
    private class Filter : IComponentRegistryCallbackBase<IAllComponentRegistry, IComponent>
    {
        // Nothing to do: a mod's change component is registered by the managed mod side and arrives here as a mod
        // component of its own.
        public void AcceptModComponent(IAllComponentRegistry registry, ModComponentInfo info, string typeFullName)
        {
        }

        public void AcceptComponent<T>(IAllComponentRegistry registry, T defaultValue = default)
            where T : struct, IComponent
        {
            if (default(T) is INetworkedComponent netComp)
            {
                registry.RegisterComponent(netComp.GetChangeComponent());
            }
        }
    }

    public void Register(IAllComponentRegistry registry)
    {
        if (serverSide.IsServerSide)
        {
            registry.RegisterFilter(new Filter());
        }
    }
}
