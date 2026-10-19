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
        Cat.CatAnimator catAnimator;
        int napTotal, viewTotal, scratchTotal, giftTotal;
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
            catAnimator = cat != null ? cat.GetComponent<Cat.CatAnimator>() : null;
            napTotal = FindObjectsByType<NapSpot>(FindObjectsSortMode.None).Length;
            viewTotal = FindObjectsByType<Viewpoint>(FindObjectsSortMode.None).Length;
            scratchTotal = FindObjectsByType<ScratchSpot>(FindObjectsSortMode.None).Length;
            giftTotal = FindObjectsByType<Gift>(FindObjectsSortMode.None).Length;
        }

        /// <summary>
        /// Drahomil's notes: everything there is to find and how much is found, shown on the board panel while the
        /// cat sits a while (research round 75: a completion list helps players who play for 100%).
        /// </summary>
        string Notes(Progress p)
        {
            int caught = 0;
            foreach (var m in mice) caught += p.BountyCount(m.DisplayName);
            var sb = new System.Text.StringBuilder("DRAHOMIL'S NOTES\n");
            sb.Append($"\nFeathers: {p.CollectedCount}/{p.TotalCollectables}");
            // A finished set names its reward; an open one hints that the full set is worth it (round 93).
            string Reward(bool done, string what) => done ? $"  ({what})" : "  (all: a surprise)";
            sb.Append($"\nNap spots: {p.NapCount}/{napTotal}{Reward(p.NapCount >= napTotal, "well rested: higher jumps")}");
            sb.Append($"\nViewpoints: {p.ViewCount}/{viewTotal}{Reward(p.ViewCount >= viewTotal, "sure-footed in the wind")}");
            sb.Append($"\nScratching places: {p.ScratchCount}/{scratchTotal}{Reward(p.ScratchCount >= scratchTotal, "sharp claws: faster climbing")}");
            if (Draho.Story.StoryDirector.InEpilogue)
                sb.Append($"\nGifts for the humans: {p.GiftCount}/{giftTotal}{Reward(p.GiftCount >= giftTotal, "a red collar with a bell")}");
            sb.Append($"\nMice caught: {caught}    Coins: {p.Coins}");
            sb.Append("\n\nCrouch and keep still to sniff for what is left.");
            return sb.ToString();
        }

        void Update()
        {
            var p = Progress.Instance;
            if (p != null && counters != null && p.Coins != shownCoins)
            {
                shownCoins = p.Coins;
                counters.text = $"Coins {p.Coins}   Feathers {p.CollectedCount}/{p.TotalCollectables}   Naps {p.NapCount}{(p.NapCount >= 12 ? " (rested)" : "")}{(Draho.Story.StoryDirector.InEpilogue ? $"   Gifts {p.GiftCount}/6" : "")}";
            }
            bool atBoard = boardSpot != null && cat != null
                && Vector3.Distance(new Vector3(cat.position.x, 0f, cat.position.z), new Vector3(boardSpot.position.x, 0f, boardSpot.position.z)) < 1.6f
                && Mathf.Abs(cat.position.y - boardSpot.position.y) < 2f
                && !Draho.World.Humans.AnyHome; // the board stays quiet while the humans are at the door
            bool notes = !atBoard && p != null && catAnimator != null && catAnimator.IdleTime > 10f; // only after a real rest: players stop to look around too
            if (notes && board != null) board.text = Notes(p);
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
            boardAlpha = Mathf.MoveTowards(boardAlpha, atBoard || notes ? 1f : 0f, 3f * Time.deltaTime);
            if (boardGroup != null) boardGroup.alpha = boardAlpha;
        }
    }
}
