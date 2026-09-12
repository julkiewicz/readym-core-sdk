using System;

namespace ReadyM.Sdk.Common.Input;

/// <summary>
/// Represents the buttons on a gamepad controller.
/// </summary>
[Flags]
public enum GamePadButton
{
    None,
    DPadUp,
    DPadDown,
    DPadLeft,
    DPadRight,
    Start,
    Back,
    LeftThumb,
    RightThumb,
    LeftShoulder,
    RightShoulder,
    A,
    B,
    X,
    Y,
    LeftTrigger,
    RightTrigger,
    DPadRightUp = DPadRight | DPadUp,
    DPadLeftDown = DPadLeft | DPadDown,
    DPadRightDown = DPadRight | DPadDown,
    DPadLeftUp = DPadLeft | DPadUp,
}