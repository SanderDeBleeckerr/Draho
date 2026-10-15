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
    }
}
