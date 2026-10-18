using Friflo.Engine.ECS;
using ReadyM.Api.ECS.Registry;
using ReadyM.Api.ECS.Worlds;
using ReadyM.Api.Multiplayer.ECS.Components;

namespace ReadyM.Api.Multiplayer.ECS.Registry;

/// <summary>Adds each networked component's change component to its archetypes. Registered on both server sides.</summary>
internal class ArchetypeChangeComponentRegistration(ServerSideSettings serverSide) : IArchetypeRegistration
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
            // A mod component on the AOT side: already handled by the managed mod side's copy of this filter.
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
