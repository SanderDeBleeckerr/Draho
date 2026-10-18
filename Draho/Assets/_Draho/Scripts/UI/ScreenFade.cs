using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>
    /// A short fade to black and back, for a jump in place or time (the hatch, the night passing).
    /// Created on first use: a full-screen black image on its own canvas, above the HUD.
    /// </summary>
    public class ScreenFade : MonoBehaviour
    {
        static ScreenFade instance;
        Image black;

        /// <summary>Fade out, call <paramref name="atBlack"/>, wait a moment, fade in.</summary>
        public static void Run(Action atBlack, float hold = 0.6f)
        {
            if (instance == null)
            {
                var go = new GameObject("ScreenFade", typeof(Canvas), typeof(CanvasGroup));
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                go.GetComponent<CanvasGroup>().blocksRaycasts = false;
                var img = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(go.transform, false);
                var rt = (RectTransform)img.transform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                img.color = new Color(0f, 0f, 0f, 0f);
                img.raycastTarget = false;
                instance = go.AddComponent<ScreenFade>();
                instance.black = img;
            }
            instance.StopAllCoroutines();
            instance.StartCoroutine(instance.Fade(atBlack, hold));
        }

        IEnumerator Fade(Action atBlack, float hold)
        {
            for (float a = black.color.a; a < 1f; a += Time.deltaTime / 0.7f) { Set(a); yield return null; }
            Set(1f);
            atBlack?.Invoke();
            yield return new WaitForSeconds(hold);
            for (float a = 1f; a > 0f; a -= Time.deltaTime / 1.1f) { Set(a); yield return null; }
            Set(0f);
        }

        void Set(float a) => black.color = new Color(0f, 0f, 0f, a);
    }
}
