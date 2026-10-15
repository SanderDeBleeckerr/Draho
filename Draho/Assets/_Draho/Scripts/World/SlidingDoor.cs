using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// A sliding glass door that opens when the cat waits in front of it (inside or outside),
    /// like a person who sees the cat at the door. It closes again when the cat has gone.
    /// </summary>
    public class SlidingDoor : MonoBehaviour
    {
        [SerializeField] Transform slider;
        [Tooltip("World-space movement of the slider from closed to open.")]
        [SerializeField] Vector3 openOffset;
        [Tooltip("World-space areas in front of the door (room side and garden side).")]
        [SerializeField] Bounds[] waitZones;
        [SerializeField] float waitToOpen = 0.8f;
        [SerializeField] float closeDelay = 3f;
        [Tooltip("Open/close speed, in door lengths per second.")]
        [SerializeField] float speed = 0.8f;

        CatMotor cat;
        Vector3 closedPosition;
        float open;        // 0 = closed, 1 = open
        float waitTimer;
        float awayTimer;

        public bool IsOpen => open > 0.99f;

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Transform movingPart, Vector3 offset, Bounds[] zones)
        {
            slider = movingPart;
            openOffset = offset;
            waitZones = zones;
        }

        void Start()
        {
            closedPosition = slider.position;
            cat = FindAnyObjectByType<CatMotor>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool near = cat != null && IsNear(cat.transform.position);
            waitTimer = near ? waitTimer + dt : 0f;
            awayTimer = near ? 0f : awayTimer + dt;

            bool opening = open > 0f ? awayTimer < closeDelay : waitTimer >= waitToOpen;
            open = Mathf.MoveTowards(open, opening ? 1f : 0f, speed * dt);
            float eased = open * open * (3f - 2f * open);
            slider.position = closedPosition + openOffset * eased;
        }

        bool IsNear(Vector3 position)
        {
            foreach (var zone in waitZones)
                if (zone.Contains(position)) return true;
            return false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.4f);
            if (waitZones == null) return;
            foreach (var zone in waitZones) Gizmos.DrawCube(zone.center, zone.size);
        }
    }
}
