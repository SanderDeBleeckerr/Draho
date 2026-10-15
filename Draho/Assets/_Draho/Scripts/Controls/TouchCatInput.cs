using Draho.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Draho.Controls
{
    /// <summary>
    /// Player input: on-screen joystick and buttons on the phone, keyboard in the editor.
    /// Pushing the joystick past <see cref="runThreshold"/> makes the cat run.
    /// A tap on the joystick jumps (or rears up when the cat stands still). Holding the middle of the
    /// joystick stands the cat up and climbs up a climbable wall.
    /// Keyboard: WASD/arrows move, Shift runs, Space jumps, C or Ctrl switches crouch on/off.
    /// </summary>
    public class TouchCatInput : MonoBehaviour, ICatInput
    {
        [SerializeField] VirtualJoystick joystick;
        [SerializeField] TouchButton crouchButton;
        [SerializeField, Range(0.5f, 1f)] float runThreshold = 0.85f;

        int seenTaps;
        bool jumpQueued;
        bool keyboardCrouch;
        Vector2 testMove;
        bool testRun;
        float testUntil;
        float testHoldUntil;

        public Vector2 Move { get; private set; }
        public bool Run { get; private set; }
        public bool HoldUp => (joystick != null && joystick.HeldCenter) || Time.time < testHoldUntil;
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

        // ------------------------------------------------------------------ test hooks
        // The playtest (Tools/Playtest) calls these with unityInstance.SendMessage('PlayerInput', ...),
        // so its input does not depend on browser focus or frame rate.

        /// <summary>"x,y,run,seconds": push the stick for some seconds, for example "0,1,0,2.5".</summary>
        public void TestMove(string args)
        {
            var v = System.Array.ConvertAll(args.Split(','), s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture));
            testMove = new Vector2(v[0], v[1]);
            testRun = v[2] > 0.5f;
            testUntil = Time.time + v[3];
        }

        public void TestJump() => jumpQueued = true;
        /// <summary>Hold the middle of the stick for some seconds, for example "3".</summary>
        public void TestHold(string seconds) =>
            testHoldUntil = Time.time + float.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture);
        public void TestCrouch() => keyboardCrouch = !keyboardCrouch;

        void Update()
        {
            var keyboard = Keyboard.current;

            if (joystick != null && joystick.Taps != seenTaps)
            {
                seenTaps = joystick.Taps;
                jumpQueued = true;
            }
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) jumpQueued = true;
                if (keyboard.cKey.wasPressedThisFrame || keyboard.leftCtrlKey.wasPressedThisFrame)
                    keyboardCrouch = !keyboardCrouch;
            }

            if (Time.time < testUntil)
            {
                Move = testMove;
                Run = testRun;
                return;
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
