using Friflo.Engine.ECS;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.Multiplayer.ECS.Components;

namespace ReadyM.Api.Multiplayer.ECS.Registry;

internal class ChangeComponentRegistration(ServerSideSettings serverSide) : IAllComponentRegistration
{
    private class Filter : IComponentRegistryCallbackBase<IAllComponentRegistry, IComponent>
    {
        // Nothing to do, rather than unsupported. This is the native server side, where a change component is
        // derived from a managed type via INetworkedComponent.GetChangeComponent, and a mod component has no
        // managed type to derive one from.
        //
        // It is not needed either. A mod that wants a change component declares a nested ChangeComponent, and
        // the mod SDK registers that itself through RegisterLocalComponent, so it reaches the host as a mod
        // component in its own right and arrives at THIS acceptor, not at AcceptComponent below.
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
