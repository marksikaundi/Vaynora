using UnityEngine;

namespace Vaynora.Player
{
    /// <summary>
    /// Movement and look commands for the player.
    /// PlayerMotor and PlayerCameraRig read this contract.
    /// A future on-screen joystick implements the same contract and can replace the device reader.
    /// </summary>
    public interface IPlayerInput
    {
        /// <summary>Camera-space move intent. x is right, y is forward. Magnitude is 0 to 1.</summary>
        Vector2 Move { get; }

        /// <summary>
        /// Look intent. Pointer devices report a pixel delta for this frame.
        /// Sticks report a deflection from -1 to 1.
        /// </summary>
        Vector2 Look { get; }

        /// <summary>True when Look is a per-frame pixel delta rather than a stick deflection.</summary>
        bool LookIsPointerDelta { get; }

        /// <summary>True while the run control is held.</summary>
        bool SprintHeld { get; }

        /// <summary>
        /// Returns true on the frame the jump control was pressed, then clears the press.
        /// PlayerMotor is the only caller.
        /// </summary>
        bool ConsumeJumpPress();
    }
}
