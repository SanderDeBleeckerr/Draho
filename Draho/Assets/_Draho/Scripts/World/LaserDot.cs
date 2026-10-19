using Draho.Cat;
using Draho.Core;
using Draho.Story;
using Draho.UI;
using UnityEngine;

namespace Draho.World
{
    /// <summary>
    /// The humans play with a laser pointer in the living room (the epilogue, by day and evening). A red dot darts
    /// over the floor, stops, wiggles and darts on (research round 79: laser play works because the dot moves like
    /// small prey, in quick darts with pauses; good play ends with a "catch", so after the last pounce the dot rests
    /// on a treat). Pounce on it: land from a jump within reach. Five catches end a game.
    /// </summary>
    public class LaserDot : MonoBehaviour
    {
        [SerializeField] Rect room = new Rect(0.4f, 0.5f, 3.8f, 3.4f); // x, z of the open floor
        [SerializeField] Transform dot;
        [SerializeField] Light glow;
        [SerializeField] float reach = 0.22f;

        CatMotor cat;
        Vector3 target;
        float pause, rest;
        int catches;
        bool wasAirborne, playing;

        public void Configure(Transform dotMesh, Light dotLight)
        {
            dot = dotMesh;
            glow = dotLight;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            Show(false);
        }

        void Show(bool on)
        {
            if (dot != null) dot.gameObject.SetActive(on);
            if (glow != null) glow.enabled = on;
        }

        void Update()
        {
            if (cat == null || dot == null) return;
            Vector3 c = cat.transform.position;
            bool inRoom = room.Contains(new Vector2(c.x, c.z)) && c.y < 1f;
            bool can = StoryDirector.InEpilogue && SkyMood.NightAmount < 0.6f && rest <= 0f;
            rest -= Time.deltaTime;
            if (!playing)
            {
                if (can && inRoom) Begin();
                return;
            }
            if (!inRoom) { End(false); return; }

            // Quick darts with pauses and a little wiggle, like a beetle.
            pause -= Time.deltaTime;
            if (pause <= 0f && (dot.position - target).sqrMagnitude < 0.0004f)
            {
                target = RandomPoint();
                pause = Random.Range(0.4f, 1.6f);
            }
            if (pause > 0f && (dot.position - target).sqrMagnitude < 0.0004f)
                dot.position = target + new Vector3(Mathf.Sin(Time.time * 31f), 0f, Mathf.Cos(Time.time * 27f)) * 0.012f;
            else
                dot.position = Vector3.MoveTowards(dot.position, target, 2.6f * Time.deltaTime);

            // A pounce: the landing after a jump, within reach of the dot.
            bool airborne = cat.IsAirborne;
            if (wasAirborne && !airborne)
            {
                Vector3 d = dot.position - c;
                if (new Vector2(d.x, d.z).magnitude < reach + 0.15f) Catch();
            }
            wasAirborne = airborne;
        }

        void Begin()
        {
            playing = true;
            catches = 0;
            dot.position = RandomPoint();
            target = dot.position;
            pause = 1f;
            Show(true);
            ProgressHud.Toast("A red dot appears on the floor! One of your humans holds a little light. Pounce on it!");
            Debug.Log("STORY laser game starts");
        }

        void Catch()
        {
            catches++;
            Debug.Log($"STORY laser caught ({catches}/5)");
            if (catches >= 5) { End(true); return; }
            ProgressHud.Toast($"Got it! ...no, it is on your paw. And now over there! ({catches}/5)");
            target = RandomPoint();
            pause = 0f;
        }

        void End(bool won)
        {
            playing = false;
            Show(false);
            rest = won ? 90f : 20f;
            if (!won) return;
            var p = Progress.Instance;
            p?.AddCoins(10, "laser game");
            ProgressHud.Toast("The dot stops on a treat. \"Good hunting, Drahomil!\" Crunch. (+10 coins)");
            Debug.Log("STORY laser game won");
        }

        Vector3 RandomPoint()
        {
            // Anywhere on the open floor, not under the coffee table (the cat cannot pounce there).
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(Random.Range(room.xMin, room.xMax), 0.012f, Random.Range(room.yMin, room.yMax));
                if (!(p.x > 1.9f && p.x < 3.3f && p.z > 2.3f && p.z < 3.7f)) return p;
            }
            return new Vector3(1.2f, 0.012f, 2.4f);
        }

        /// <summary>Test hook: SendMessage('LaserDot', 'TestCatch'): count a pounce.</summary>
        public void TestCatch() { if (playing) Catch(); }
    }
}
