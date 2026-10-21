using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ReadyM.Api.Multiplayer.Interop;
using Yooni.Native.Container;

namespace ReadyM.Relay.Server.Sdk.Ecs.Components;

/// <summary>
/// What is left of the 0.x registry: forwarding the 1.0 shapes' extensions of the game's archetypes.
/// </summary>
// FixMe(2026-10-18-twins-m4): the extensions cross through a pointer, not as archetype registrations. Deleted then.
internal sealed class ModComponentRegistryFixMe(ModComponentRegistryFixMe.NativeBinding binding, ILogger logger)
{
    /// <summary>What it calls on the AOT side: ModArchetypeExtensions.Add.</summary>
    public struct NativeBinding
    {
        public required IntPtr AddArchetypeExtension;
    }

    private readonly AddArchetypeExtensionDelegate? _addArchetypeExtension =
        binding.AddArchetypeExtension == IntPtr.Zero
            ? null
            : Marshal.GetDelegateForFunctionPointer<AddArchetypeExtensionDelegate>(binding.AddArchetypeExtension);

    /// <summary>
    /// Adds components to an archetype the game registered, named by the shape it is declared as so a mod never holds an archetype id.
    /// </summary>
    public void AddArchetypeExtensions(Type shape, IReadOnlyList<Type> components)
    {
        if (components.Count == 0 || _addArchetypeExtension is null)
            return;

        logger.LogDebug("Adding {Components} to {Shape}", components, shape.FullName);

        var shapeName = new NativeString256(shape.FullName, false);

        foreach (var component in components)
            _addArchetypeExtension(shapeName, new NativeString256(component.FullName, false));
    }
}
