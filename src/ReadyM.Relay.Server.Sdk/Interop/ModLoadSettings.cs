using Yooni.Native.Container;

namespace ReadyM.Relay.Server.Sdk.Interop;

/// <exclude/>
/// <remarks>What the managed mod side needs to load the mods.</remarks>
public struct ModLoadSettings
{
    public required NativeString256 ModDirectory;
}
