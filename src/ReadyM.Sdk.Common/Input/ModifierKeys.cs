using System;

namespace ReadyM.Sdk.Common.Input;

/// <summary>
/// Represents modifier keys that can be pressed in combination with other keys.
/// </summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4
}