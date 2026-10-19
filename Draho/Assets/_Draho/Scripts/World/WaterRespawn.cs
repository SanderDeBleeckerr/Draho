using Draho.Cat;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The river. A cat that falls in scrambles out on the nearest quay (the east bank), wet but safe.
    /// </summary>
    public class WaterRespawn : MonoBehaviour
    {
        [SerializeField] Rect area;          // x, z of the water
        [SerializeField] float surfaceY = -1f;
        [Tooltip("The quay runs between these z values (the cat climbs out only there).")]
        [SerializeField] Vector2 quayZ = new Vector2(-111.5f, 4.5f);

        CatMotor cat;

        /// <summary>Set up by the level builder (edit time).</summary>
        public void Configure(Rect waterArea, float surface)
        {
            area = waterArea;
            surfaceY = surface;
        }

        void Awake() => cat = FindAnyObjectByType<CatMotor>();

        void LateUpdate()
        {
            if (cat == null) return;
            Vector3 p = cat.transform.position;
            if (p.y > surfaceY - 0.1f || !area.Contains(new Vector2(p.x, p.z))) return;
            // Out on the east quay, at the same place along the river, facing away from the water.
            var to = new Vector3(area.xMax + 0.22f, 0.05f, Mathf.Clamp(p.z, quayZ.x, quayZ.y));
            cat.Teleport(to, Quaternion.Euler(0f, 90f, 0f));
            Debug.Log($"CAT splash! Out of the river at z {p.z:0.0}");
            cat.GetComponent<CatShake>()?.Shake();
        }
    }
}
