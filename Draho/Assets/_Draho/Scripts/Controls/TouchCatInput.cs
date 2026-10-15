using Draho.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draho.Controls
{
    /// <summary>
    /// Player input: on-screen joystick and buttons on the phone, keyboard in the editor.
    /// Pushing the joystick past <see cref="runThreshold"/> makes the cat run.
    /// Keyboard: WASD/arrows move, Shift runs, Space jumps, C or Ctrl switches crouch on/off.
    /// </summary>
    public class TouchCatInput : MonoBehaviour, ICatInput
    {
        [SerializeField] VirtualJoystick joystick;
        [SerializeField] TouchButton jumpButton;
        [SerializeField] TouchButton crouchButton;
        [SerializeField, Range(0.5f, 1f)] float runThreshold = 0.85f;

        int seenJumpPresses;
        bool jumpQueued;
        bool keyboardCrouch;

        public Vector2 Move { get; private set; }
        public bool Run { get; private set; }
        public bool Crouch => keyboardCrouch || (crouchButton != null && crouchButton.IsOn);

        public bool ConsumeJump()
        {
            bool jump = jumpQueued;
            jumpQueued = false;
            return jump;
        }

        public void CancelCrouch()
        {
            keyboardCrouch = false;
            if (crouchButton != null) crouchButton.SetOn(false);
        }

        void Update()
        {
            var keyboard = Keyboard.current;

            if (jumpButton != null && jumpButton.Presses != seenJumpPresses)
            {
                seenJumpPresses = jumpButton.Presses;
                jumpQueued = true;
            }
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) jumpQueued = true;
                if (keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame)
                    keyboardCrouch = !keyboardCrouch;
            }

            Vector2 stick = joystick != null ? joystick.Value : Vector2.zero;
            if (stick.sqrMagnitude > 0f)
            {
                Run = stick.magnitude >= runThreshold;
                Move = Vector2.ClampMagnitude(stick / runThreshold, 1f);
                return;
            }

            if (keyboard == null)
            {
                Move = Vector2.zero;
                Run = false;
                return;
            }

            Vector2 keys = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keys.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keys.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keys.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keys.x -= 1f;
            Move = Vector2.ClampMagnitude(keys, 1f);
            Run = keyboard.leftShiftKey.isPressed && keys != Vector2.zero;
        }
    }
}
