using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>
    /// On-screen button for touch. <see cref="Presses"/> counts every press, so a reader can see a tap
    /// even when press and release happen in the same frame. Optional toggle mode (for crouch).
    /// </summary>
    public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] bool toggle;
        [SerializeField] Image icon;
        [SerializeField] Color normalColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] Color activeColor = new Color(1f, 0.85f, 0.3f, 0.8f);

        int pointerCount;

        /// <summary>Total number of presses since the button was created.</summary>
        public int Presses { get; private set; }
        /// <summary>Held down (normal mode) or switched on (toggle mode).</summary>
        public bool IsOn { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            Presses++;
            pointerCount++;
            IsOn = toggle ? !IsOn : true;
            Refresh();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerCount = Mathf.Max(0, pointerCount - 1);
            if (!toggle) IsOn = pointerCount > 0;
            Refresh();
        }

        /// <summary>Sets the toggle state from code (for example, a jump ends the crouch).</summary>
        public void SetOn(bool on)
        {
            IsOn = on;
            Refresh();
        }

        void OnDisable()
        {
            pointerCount = 0;
            if (!toggle) IsOn = false;
            Refresh();
        }

        void Refresh()
        {
            if (icon != null) icon.color = IsOn ? activeColor : normalColor;
        }
    }
}
