using UnityEngine;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>The story line and the current goal at the top of the screen.</summary>
    public class StoryHud : MonoBehaviour
    {
        [SerializeField] Text story;
        [SerializeField] Text goal;
        [SerializeField] CanvasGroup group;
        [SerializeField] float fadeSpeed = 1.5f;

        float alpha;

        public void Show(string storyText, string goalText)
        {
            story.text = storyText;
            goal.text = string.IsNullOrEmpty(goalText) ? "" : "GOAL: " + goalText;
            alpha = 0f;
        }

        void Update()
        {
            alpha = Mathf.MoveTowards(alpha, 1f, fadeSpeed * Time.deltaTime);
            group.alpha = alpha;
        }
    }
}
