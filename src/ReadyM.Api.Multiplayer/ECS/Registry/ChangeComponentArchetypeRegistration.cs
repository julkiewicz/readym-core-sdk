using Friflo.Engine.ECS;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Multiplayer.ECS.Components;

namespace ReadyM.Api.Multiplayer.ECS.Registry;

/// <summary>
/// Puts the change component of every networked component on each archetype that carries it. Registered on both
/// sides of the server: the AOT side's archetypes and the managed mod side's.
/// </summary>
internal class ChangeComponentArchetypeRegistration(ServerSideSettings serverSide) : IArchetypeRegistration
{
    private class Filter : IArchetypeBuilderCallback
    {
        public void AcceptComponentType<T>(ArchetypeBuilder builder) where T : struct, IComponent
        {
            if (default(T) is INetworkedComponent netComp)
            {
                builder.Add(netComp.GetChangeComponent());
            }
        }

        public void AcceptComponentType<T>(ArchetypeBuilder builder, T defaultValue) where T : struct, IComponent
        {
            if (default(T) is INetworkedComponent netComp)
            {
                builder.Add(netComp.GetChangeComponent());
            }
        }

        public void AcceptStrideComponent(ArchetypeBuilder builder, int structIndex, int stride)
        {
            // A stride component is a mod component seen from the AOT side. The managed mod side registers this same
            // filter on its own archetypes, which is where a mod component's change component is put on.
        }

        public void AcceptTag<T>(ArchetypeBuilder builder) where T : struct, ITag
        {
            // no-op
        }

        public void AcceptTags(ArchetypeBuilder builder, in Friflo.Engine.ECS.Tags tags)
        {
            // no-op
        }
    }

    public void Register(IArchetypeRegistry registry)
    {
        if (serverSide.IsServerSide)
        {
            registry.RegisterFilter(new Filter());
        }
    }
}
