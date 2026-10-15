using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A surface the cat can climb (tree trunk, wooden shed wall). The cat stands still in front of it
    /// and taps the stick: it stands up on its hind legs, then starts to climb.
    /// </summary>
    public class Climbable : MonoBehaviour
    {
        [Tooltip("World height of the top. The cat stops here, or climbs onto the top.")]
        [SerializeField] float topY = 1.7f;
        [Tooltip("True when the top is flat and wide enough to stand on (a roof, a wall).")]
        [SerializeField] bool canStandOnTop = true;

        public float TopY => topY;
        public bool CanStandOnTop => canStandOnTop;

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(float top, bool standOnTop)
        {
            topY = top;
            canStandOnTop = standOnTop;
        }
    }
}
