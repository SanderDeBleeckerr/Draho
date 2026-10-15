using UnityEngine;
using UnityEngine.EventSystems;

namespace Draho.UI
{
    /// <summary>
    /// Floating joystick. Put it on a transparent UI area (for example the left half of the screen).
    /// The stick appears where the thumb touches down. The area's pivot and the background's anchors
    /// must both be bottom-left.
    /// A short tap without a push is the jump button (<see cref="Taps"/>). So that a tap does not also
    /// move the cat, a small push only counts as movement after <see cref="tapTime"/>.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;

        [SerializeField] RectTransform background;
        [SerializeField] RectTransform knob;
        [SerializeField] float radius = 90f;
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.08f;
        [Tooltip("A touch shorter than this (seconds) that stays near the centre is a tap.")]
        [SerializeField] float tapTime = 0.22f;
        [SerializeField, Range(0f, 1f)] float tapMaxOffset = 0.3f;

        RectTransform area;
        Vector2 restPosition;
        int pointerId = NoPointer;
        Vector2 raw;
        float pressTime;
        int pressFrame;
        float maxOffset;

        /// <summary>Stick value, magnitude 0..1.</summary>
        public Vector2 Value
        {
            get
            {
                if (pointerId == NoPointer || raw.magnitude < deadZone) return Vector2.zero;
                bool maybeTap = Time.unscaledTime - pressTime < tapTime && maxOffset < tapMaxOffset;
                return maybeTap ? Vector2.zero : raw;
            }
        }

        /// <summary>Counts taps on the stick; the input reads a change as a jump.</summary>
        public int Taps { get; private set; }

        void Awake()
        {
            area = (RectTransform)transform;
            restPosition = background.anchoredPosition;
        }

        void OnDisable() => Release();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != NoPointer) return;
            pointerId = eventData.pointerId;
            pressTime = Time.unscaledTime;
            pressFrame = Time.frameCount;
            maxOffset = 0f;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area, eventData.position, eventData.pressEventCamera, out var local))
            {
                background.anchoredPosition = local;
            }
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    area, eventData.position, eventData.pressEventCamera, out var local)) return;

            Vector2 offset = Vector2.ClampMagnitude(local - background.anchoredPosition, radius);
            knob.anchoredPosition = offset;
            raw = offset / radius;
            maxOffset = Mathf.Max(maxOffset, raw.magnitude);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            // Short in time, or within two frames on a slow device, and no real push: a tap.
            bool quick = Time.unscaledTime - pressTime < tapTime || Time.frameCount - pressFrame <= 2;
            if (quick && maxOffset < tapMaxOffset) Taps++;
            Release();
        }

        void Release()
        {
            pointerId = NoPointer;
            raw = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (background != null) background.anchoredPosition = restPosition;
        }
    }
}
