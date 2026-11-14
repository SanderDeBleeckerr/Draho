using UnityEngine;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>
    /// A short line in the top-right corner for side moments (rivals, finds, friends), fading out after a few seconds.
    /// </summary>
    public class ProgressHud : MonoBehaviour
    {
        [SerializeField] Text toast;

        static ProgressHud instance;
        float toastTime;

        public void Configure(Text toastText) => toast = toastText;

        /// <summary>A short message in the corner (side events: rivals, finds, friends).</summary>
        public static void Toast(string message)
        {
            Debug.Log($"TOAST {message}");
            if (instance == null || instance.toast == null) return;
            instance.toast.text = message;
            instance.toastTime = 5f;
        }

        void Awake()
        {
            instance = this;
            if (toast != null) toast.text = "";
        }

        void Update()
        {
            if (toast == null) return;
            toastTime -= Time.deltaTime;
            var c = toast.color;
            c.a = Mathf.Clamp01(toastTime);
            toast.color = c;
        }
    }
}
