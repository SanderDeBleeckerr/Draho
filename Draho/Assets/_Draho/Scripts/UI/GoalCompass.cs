using System;
using UnityEngine;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>
    /// A small chevron under the story line that points to the current goal (relative to the camera),
    /// with the distance. It hides when the goal is near (closer than 8 m) or when the beat has no place.
    /// Research: players accept on-screen help that only gives a direction; too much guidance bores.
    /// </summary>
    public class GoalCompass : MonoBehaviour
    {
        [SerializeField] RectTransform arrow;
        [SerializeField] Text distance;
        [SerializeField] CanvasGroup group;
        [SerializeField] float hideWithin = 8f;

        public static Func<Vector3?> Target;
        Transform cat, cam;
        float alpha;

        public void Configure(RectTransform arrowPart, Text distanceText, CanvasGroup canvasGroup)
        {
            arrow = arrowPart;
            distance = distanceText;
            group = canvasGroup;
        }

        void Start()
        {
            cat = FindAnyObjectByType<Cat.CatMotor>()?.transform;
            cam = Camera.main != null ? Camera.main.transform : null;
        }

        void Update()
        {
            Vector3? target = Target?.Invoke();
            bool show = false;
            if (target.HasValue && cat != null && cam != null)
            {
                Vector3 to = target.Value - cat.position;
                float flat = new Vector2(to.x, to.z).magnitude;
                show = flat > hideWithin;
                if (show)
                {
                    Vector3 fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                    float angle = Vector3.SignedAngle(fwd, new Vector3(to.x, 0f, to.z), Vector3.up);
                    arrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
                    distance.text = Mathf.Abs(to.y) > 2f ? $"{flat:0} m  {(to.y > 0 ? "up" : "down")}" : $"{flat:0} m";
                }
            }
            alpha = Mathf.MoveTowards(alpha, show ? 1f : 0f, 2f * Time.deltaTime);
            if (group != null) group.alpha = alpha;
        }
    }
}
