using UnityEngine;

namespace Draho.Controls
{
    /// <summary>
    /// What the cat wants to do this frame. Touch, keyboard, AI or replays can all provide it.
    /// </summary>
    public interface ICatInput
    {
        /// <summary>Move direction relative to the camera (x = right, y = forward), magnitude 0..1.</summary>
        Vector2 Move { get; }

        /// <summary>True when the cat should run instead of walk.</summary>
        bool Run { get; }

        /// <summary>True while the cat should stay low (duck / sneak).</summary>
        bool Crouch { get; }

        /// <summary>True once per jump press. Reading it clears it.</summary>
        bool ConsumeJump();

        /// <summary>True while the player holds the middle of the stick: stand up, and climb up on a wall.</summary>
        bool HoldUp { get; }

        /// <summary>Tells the input that the crouch ended (for example because the cat jumped).</summary>
        void CancelCrouch();
    }
}
