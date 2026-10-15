using UnityEngine;
using UnityEngine.EventSystems;

namespace Draho.UI
{
    /// <summary>
    /// Floating joystick. Put it on a transparent UI area (for example the left half of the screen).
    /// The stick appears where the thumb touches down. The area's pivot and the background's anchors
    /// must both be bottom-left.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;

        [SerializeField] RectTransform background;
        [SerializeField] RectTransform knob;
        [SerializeField] float radius = 90f;
        [SerializeField, Range(0f, 0.5f)] float deadZone = 0.08f;

        RectTransform area;
        Vector2 restPosition;
        int pointerId = NoPointer;

        /// <summary>Stick value, magnitude 0..1.</summary>
        public Vector2 Value { get; private set; }

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
            Vector2 value = offset / radius;
            Value = value.magnitude < deadZone ? Vector2.zero : value;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Release();
        }

        void Release()
        {
            pointerId = NoPointer;
            Value = Vector2.zero;
            if (knob != null) knob.anchoredPosition = Vector2.zero;
            if (background != null) background.anchoredPosition = restPosition;
        }
    }
}
