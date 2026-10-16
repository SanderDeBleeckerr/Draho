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
            boardAlpha = Mathf.MoveTowards(boardAlpha, atBoard ? 1f : 0f, 3f * Time.deltaTime);
            if (boardGroup != null) boardGroup.alpha = boardAlpha;
        }
    }
}
