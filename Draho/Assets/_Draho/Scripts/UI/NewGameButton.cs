using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>
    /// A small "New game" text in the top-left corner. The first tap asks to confirm ("Tap again: new game"); a
    /// second tap within 4 s deletes the save and starts again at the beginning, in the apartment.
    /// </summary>
    public class NewGameButton : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] Text label;

        float armedUntil = -1f;

        public void Configure(Text text) => label = text;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime < armedUntil)
            {
                Debug.Log("SAVE new game");
                Draho.Core.Progress.Instance?.NewGame();
                return;
            }
            armedUntil = Time.unscaledTime + 4f;
        }

        void Update()
        {
            if (label != null) label.text = Time.unscaledTime < armedUntil ? "Tap again: new game" : "New game";
        }
    }
}
