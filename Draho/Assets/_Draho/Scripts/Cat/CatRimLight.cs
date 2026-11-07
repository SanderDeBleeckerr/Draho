using UnityEngine;

namespace Draho.Cat
{
    /// <summary>
    /// Keeps a small light behind the cat, on the far side from the camera, so the black fur gets a soft outline
    /// against dark backgrounds (docs/ART_STYLE.md §4: a rim light, not a glow). One short-range point light
    /// without shadows: cheap on a phone.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class CatRimLight : MonoBehaviour
    {
        [SerializeField] Transform cat;
        [SerializeField] Transform view;
        [SerializeField] float behind = 0.5f;    // metres past the cat, away from the camera
        [SerializeField] float height = 0.45f;   // a little above the back, so ears and spine catch it

        void LateUpdate()
        {
            if (cat == null || view == null) return;
            Vector3 away = cat.position - view.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : cat.forward;
            transform.position = cat.position + away * behind + Vector3.up * height;
        }
    }
}
