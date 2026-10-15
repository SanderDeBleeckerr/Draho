using UnityEngine;
using UnityEngine.EventSystems;

namespace Draho.UI
{
    /// <summary>
    /// Transparent UI area that collects swipe movement for the camera (one finger at a time).
    /// </summary>
    public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        const int NoPointer = int.MinValue;

        int pointerId = NoPointer;
        Vector2 pending;

        /// <summary>Returns the swipe movement in pixels since the last call.</summary>
        public Vector2 ConsumeDelta()
        {
            Vector2 delta = pending;
            pending = Vector2.zero;
            return delta;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId == NoPointer) pointerId = eventData.pointerId;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) pending += eventData.delta;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) pointerId = NoPointer;
        }

        void OnDisable()
        {
            pointerId = NoPointer;
            pending = Vector2.zero;
        }
    }
}
