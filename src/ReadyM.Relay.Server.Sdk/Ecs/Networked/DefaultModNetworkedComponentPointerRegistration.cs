using System.Runtime.InteropServices;
using ReadyM.Api.Multiplayer.ECS.Components;
using ReadyM.Api.Multiplayer.ECS.Registry;
using ReadyM.Api.Multiplayer.Interop;
using ReadyM.Relay.Server.Sdk.Interop;
using Yooni.Native.Container;

namespace ReadyM.Relay.Server.Sdk.Ecs.Networked;

/// <summary>
/// Builds the replication callbacks of every networked component the mods declared and hands each to the relay.
/// </summary>
internal sealed class DefaultModNetworkedComponentPointerRegistration(
    AotPointers aotPointers,
    ModComponentManager componentManager) : IModNetworkedComponentPointerRegistration
{
    private sealed class RegisterPointersCallback(
        ModComponentManager componentManager,
        RegisterModNetworkedComponentDelegate registerWithRelay,
        ModNetworkedComponentPointerRegistry pointerRegistry) : INetworkedComponentRegistryCallback
    {
        public void AcceptComponent<T>(INetworkedComponentRegistry registry, T defaultValue = default)
            where T : struct, INetworkedComponent
        {
            var typeFullName = typeof(T).FullName!;
            var info = componentManager.RegisterNetworkedComponent<T>(registry.GetNetworkedComponentDeliveryMethod<T>());

            pointerRegistry.Register(typeFullName, info);
            registerWithRelay(info, new NativeString256(typeFullName, false));
        }

        public void AcceptModComponent(INetworkedComponentRegistry registry, string typeFullName)
            => throw new NotSupportedException(
                $"{nameof(AcceptModComponent)} is not supported on the mod side: every component here is the mod's own. "
                + $"Offending component: {typeFullName}.");
    }

    public void Register(INetworkedComponentRegistry registry, ModNetworkedComponentPointerRegistry pointerRegistry)
    {
        var registerWithRelay = Marshal.GetDelegateForFunctionPointer<RegisterModNetworkedComponentDelegate>(
            aotPointers.RegisterModNetworkedComponent);

        registry.Accept(new RegisterPointersCallback(componentManager, registerWithRelay, pointerRegistry));
    }
}
