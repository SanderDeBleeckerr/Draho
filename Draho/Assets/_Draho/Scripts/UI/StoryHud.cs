using UnityEngine;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>
    /// The story line and the current goal at the top of the screen. A new beat shows in full long enough to read;
    /// then the panel fades away so the view (and the sky) is free, and comes back when the cat stands still for a
    /// moment (research round 73: games keep the screen clear and bring the objective back on demand or at rest).
    /// </summary>
    public class StoryHud : MonoBehaviour
    {
        [SerializeField] Text story;
        [SerializeField] Text goal;
        [SerializeField] CanvasGroup group;
        [SerializeField] float fadeSpeed = 1.5f;
        [SerializeField] float stillToShow = 2.5f;

        float alpha, readLeft, still;
        Draho.Cat.CatMotor cat;

        public void Show(string storyText, string goalText)
        {
            story.text = storyText;
            goal.text = string.IsNullOrEmpty(goalText) ? "" : "GOAL: " + goalText;
            alpha = 0f;
            // About a third of a second per word, at least 8 s.
            int words = (storyText + " " + goalText).Split(' ').Length;
            readLeft = Mathf.Max(8f, words * 0.33f);
        }

        void Update()
        {
            if (cat == null) cat = FindAnyObjectByType<Draho.Cat.CatMotor>();
            readLeft -= Time.deltaTime;
            bool resting = cat != null && cat.Speed < 0.05f && cat.IsGrounded;
            still = resting ? still + Time.deltaTime : 0f;
            bool visible = readLeft > 0f || still > stillToShow;
            alpha = Mathf.MoveTowards(alpha, visible ? 1f : 0f, fadeSpeed * (visible ? 1f : 0.5f) * Time.deltaTime);
            group.alpha = alpha;
        }
    }
}
