using System;
using Draho.Cat;
using Draho.UI;
using Draho.World;
using UnityEngine;

namespace Draho.Story
{
    /// <summary>
    /// Chapter 1, "The scratching sound": a list of story beats. Each beat shows a line and a goal,
    /// and ends when its goal is done.
    /// Test hooks in the web build URL: ?beat=2 starts at beat 2, ?at=6.5,1.2 puts the cat at x,z.
    /// </summary>
    public class StoryDirector : MonoBehaviour
    {
        [SerializeField] StoryHud hud;
        [SerializeField] Mouse mouse;
        [Tooltip("x below this value is outside, in the garden.")]
        [SerializeField] float gardenEdgeX = 0f;
        [Tooltip("Seconds before a beat can end, so the player can read the line.")]
        [SerializeField] float minBeatTime = 2f;

        struct Beat
        {
            public string Story, Goal;
            public Func<bool> Done;
            public Action Begin;
        }

        Beat[] beats;
        CatMotor cat;
        SlidingDoor door;
        int index = -1;
        float beatTime;
        float walked;
        Vector3 lastPosition;

        public void Configure(StoryHud storyHud, Mouse storyMouse, float gardenEdge)
        {
            hud = storyHud;
            mouse = storyMouse;
            gardenEdgeX = gardenEdge;
        }

        void Start()
        {
            cat = FindAnyObjectByType<CatMotor>();
            door = FindAnyObjectByType<SlidingDoor>();
            if (mouse != null) mouse.gameObject.SetActive(false);

            beats = new[]
            {
                new Beat
                {
                    Story = "Morning. The humans left for work. The apartment belongs to Drahomil now.",
                    Goal = "Explore. Push the stick far to run.",
                    Done = () => walked > 5f,
                },
                new Beat
                {
                    Story = "A good cat checks everything from up high.",
                    Goal = "JUMP onto the sofa or the cat tree.",
                    Done = () => cat.IsGrounded && cat.transform.position.y > 0.3f,
                },
                new Beat
                {
                    Story = "Scritch... scritch... Something moves in the kitchen!",
                    Goal = "Catch the mouse. DUCK to sneak up on it, it hears running paws.",
                    Begin = () => { if (mouse != null) mouse.gameObject.SetActive(true); },
                    Done = () => mouse == null || mouse.IsCaught,
                },
                new Beat
                {
                    Story = "Got it! Drahomil, mouse hunter. The first job is done.",
                    Goal = "Sit in front of the garden door until it opens.",
                    Done = () => door == null || door.IsOpen,
                },
                new Beat
                {
                    Story = "The door slides open. Fresh air, birds, and new smells.",
                    Goal = "Go out into the garden.",
                    Done = () => cat.transform.position.x < gardenEdgeX - 0.4f,
                },
                new Beat
                {
                    Story = "End of chapter 1. More jobs for Drahomil soon. Thank you for playing!",
                    Goal = "",
                    Done = () => false,
                },
            };

            ApplyUrlArguments(out int startBeat);
            lastPosition = cat.transform.position;
            for (int i = 0; i < startBeat && i < beats.Length - 1; i++) beats[i].Begin?.Invoke();
            index = Mathf.Clamp(startBeat, 0, beats.Length - 1) - 1;
            Next();
        }

        void Update()
        {
            if (cat == null || index < 0) return;
            beatTime += Time.deltaTime;
            Vector3 p = cat.transform.position;
            walked += new Vector2(p.x - lastPosition.x, p.z - lastPosition.z).magnitude;
            lastPosition = p;
            if (beatTime >= minBeatTime && beats[index].Done()) Next();
        }

        void Next()
        {
            if (index + 1 >= beats.Length) return;
            index++;
            beatTime = 0f;
            Beat beat = beats[index];
            beat.Begin?.Invoke();
            if (hud != null) hud.Show(beat.Story, beat.Goal);
            Debug.Log($"STORY beat {index}: {beat.Story}");
        }

        void ApplyUrlArguments(out int startBeat)
        {
            startBeat = 0;
            string url = Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0) return;
            foreach (string pair in url.Substring(q + 1).Split('&'))
            {
                string[] kv = pair.Split('=');
                if (kv.Length != 2) continue;
                if (kv[0] == "beat") int.TryParse(kv[1], out startBeat);
                if (kv[0] == "at")
                {
                    string[] xz = Uri.UnescapeDataString(kv[1]).Split(',');
                    if (xz.Length == 2 && float.TryParse(xz[0], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float x)
                        && float.TryParse(xz[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float z))
                        cat.Teleport(new Vector3(x, 0.05f, z), cat.transform.rotation);
                }
            }
        }
    }
}
