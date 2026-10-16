using Draho.Core;
using Draho.World;
using UnityEngine;
using UnityEngine.UI;

namespace Draho.UI
{
    /// <summary>
    /// Coins and feathers in the top-right corner, and the bounty board list when the cat stands at the board.
    /// </summary>
    public class ProgressHud : MonoBehaviour
    {
        [SerializeField] Text counters;
        [SerializeField] Text board;
        [SerializeField] CanvasGroup boardGroup;

        static ProgressHud instance;
        Text toast;
        float toastTime;
        Transform boardSpot, cat;
        Mouse[] mice;
        float boardAlpha;
        int shownCoins = -1;

        public void Configure(Text countersText, Text boardText, CanvasGroup boardPanel)
        {
            counters = countersText;
            board = boardText;
            boardGroup = boardPanel;
        }

        /// <summary>A short message under the coins (side events: naps, rivals, finds).</summary>
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
            if (counters != null)
            {
                // The toast line sits under the counters, same style.
                toast = Instantiate(counters, counters.transform.parent);
                toast.name = "Toast";
                var rt = (RectTransform)toast.transform;
                rt.anchoredPosition += new Vector2(0f, -56f);
                rt.sizeDelta = new Vector2(900f, 60f);
                toast.fontSize = 32;
                toast.text = "";
            }
        }

        void Start()
        {
            boardSpot = GameObject.Find("Marker_BountyBoard")?.transform;
            cat = FindAnyObjectByType<Cat.CatMotor>()?.transform;
            mice = FindObjectsByType<Mouse>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        void Update()
        {
            var p = Progress.Instance;
            if (p != null && counters != null && p.Coins != shownCoins)
            {
                shownCoins = p.Coins;
                counters.text = $"Coins {p.Coins}     Feathers {p.CollectedCount}/{p.TotalCollectables}";
            }
            bool atBoard = boardSpot != null && cat != null
                && Vector3.Distance(new Vector3(cat.position.x, 0f, cat.position.z), new Vector3(boardSpot.position.x, 0f, boardSpot.position.z)) < 1.6f
                && Mathf.Abs(cat.position.y - boardSpot.position.y) < 2f;
            if (atBoard && board != null)
            {
                var sb = new System.Text.StringBuilder("BOUNTY BOARD — mice wanted\n");
                foreach (var m in mice)
                    sb.Append($"\n{m.DisplayName}: {m.Bounty} coins   (caught {(p != null ? p.BountyCount(m.DisplayName) : 0)}x)");
                sb.Append("\n\nCaught mice come back, and each new one is warier.");
                board.text = sb.ToString();
            }
            if (toast != null)
            {
                toastTime -= Time.deltaTime;
                var c = toast.color;
                c.a = Mathf.Clamp01(toastTime);
                toast.color = c;
            }
            boardAlpha = Mathf.MoveTowards(boardAlpha, atBoard ? 1f : 0f, 3f * Time.deltaTime);
            if (boardGroup != null) boardGroup.alpha = boardAlpha;
        }
    }
}
