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
    /// Test hooks in the web build URL: ?beat=2 starts at beat 2, ?at=6.5,1.2 puts the cat at x,z,
    /// ?at=6.5,1.2,90 also turns it (degrees, 0 = north, 90 = east), ?at=-7,-4,180,5.8 also sets the height.
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
        Bird[] birds;
        Climbable maple;
        Hedgehog hedgehog;
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
            birds = FindObjectsByType<Bird>(FindObjectsSortMode.None);
            hedgehog = FindAnyObjectByType<Hedgehog>();
            maple = Array.Find(FindObjectsByType<Climbable>(FindObjectsSortMode.None), c => c.name == "maple");
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
                    Goal = "Run at the sofa or the cat tree, then tap the stick to jump.",
                    Done = () => cat.IsGrounded && cat.transform.position.y > 0.3f,
                },
                new Beat
                {
                    Story = "Scritch... scritch... Something moves in the kitchen!",
                    Goal = "Catch the mouse. It hears running paws: DUCK to sneak up on it.",
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
                    Story = "Blackbirds hop on the lawn. They look for worms, and they watch for cats.",
                    Goal = "Sneak up on a blackbird: switch DUCK on and creep close.",
                    Done = () => birds.Length == 0 || Array.Exists(birds, b => b.WasStalked),
                },
                new Beat
                {
                    Story = "Almost! The blackbird escapes to the maple by the fence. It sings at Drahomil from a branch.",
                    Goal = "Climb the maple: walk up to the trunk and hold the middle of the stick.",
                    Begin = SendBirdToMaple,
                    Done = () => maple == null || (cat.CurrentClimbable == maple && cat.transform.position.y > 1.2f),
                },
                new Beat
                {
                    Story = "The trunk is too thin higher up. But at the end of the garden, ivy grows up the old wall. A ladder for cats.",
                    Goal = "Walk up to the ivy, then hold your thumb in the middle of the stick to climb.",
                    Done = () => cat.IsClimbing && cat.transform.position.y > 2.6f,
                },
                new Beat
                {
                    Story = "End of chapter 1! But the ivy does not stop here. It goes all the way up to the roof.",
                    Goal = "Keep holding: climb to the top and pull yourself onto the roof.",
                    Done = () => cat.IsGrounded && cat.transform.position.y > 6f,
                },
                new Beat
                {
                    Story = "Chapter 2: The Rooftops. Warm gravel, chimneys, and the whole block below. Nobody comes up here. Except cats.",
                    Goal = "Find the drainpipe on the high wall. Climb it to the highest roof.",
                    Done = () => cat.IsGrounded && cat.transform.position.y > 8.4f,
                },
                new Beat
                {
                    Story = "The highest point of the block. Drahomil can see the garden, the maple and the blackbirds, all very small.",
                    Goal = "Find a way down to the low roof at the back.",
                    Done = () => cat.IsGrounded && cat.transform.position.y > 3.5f && cat.transform.position.y < 4f
                                 && cat.transform.position.z < -11f,
                },
                new Beat
                {
                    Story = "From up here Drahomil sees the neighbour's garden: wild and empty for months.",
                    Goal = "Go back down to your garden. Find a loose plank in the fence near the maple.",
                    Done = () => cat.transform.position.z > 5.3f && cat.transform.position.y < 1f,
                },
                new Beat
                {
                    Story = "Chapter 3: Over the Fence. The grass is as high as a cat. Something snuffles in it.",
                    Goal = "Find what is snuffling in the tall grass.",
                    Done = () => hedgehog == null || Vector3.Distance(hedgehog.transform.position, cat.transform.position) < 1.2f,
                },
                new Beat
                {
                    Story = "A hedgehog! It rolls into a prickly ball. Hedgehogs need time to trust a cat.",
                    Goal = "Sit still close to the hedgehog. Do not move.",
                    Done = () => hedgehog == null || hedgehog.Trusts,
                },
                new Beat
                {
                    Story = "The hedgehog sniffs Drahomil's nose and trots off. It wants to show something.",
                    Goal = "Follow the hedgehog. Go slowly.",
                    Done = () => hedgehog == null || (hedgehog.AtNest
                                 && Vector3.Distance(hedgehog.transform.position, cat.transform.position) < 1.6f),
                },
                new Beat
                {
                    Story = "Under the old wheelbarrow: a nest with three tiny hoglets. Drahomil keeps the secret. End of chapter 3 for now!",
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

        void SendBirdToMaple()
        {
            if (maple == null || birds.Length == 0) return;
            // The bird nearest to the maple flies to a branch just above the cat's reach.
            Vector3 branch = maple.transform.position + new Vector3(0.8f, 2.9f, -0.3f);
            Bird nearest = birds[0];
            foreach (var b in birds)
                if (Vector3.Distance(b.transform.position, branch) < Vector3.Distance(nearest.transform.position, branch)) nearest = b;
            nearest.FlyToPerch(branch);
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

        /// <summary>Test hook: unityInstance.SendMessage('StoryDirector', 'LogState') from the playtest.</summary>
        public void LogState()
        {
            Vector3 p = cat.transform.position;
            Debug.Log($"STATE beat {index} cat ({p.x:0.00}, {p.y:0.00}, {p.z:0.00}) yaw {cat.transform.eulerAngles.y:0} " +
                      $"mode {cat.ModeName} speed {cat.Speed:0.00} grounded {cat.IsGrounded} " +
                      $"mouse {(mouse == null ? "none" : mouse.IsCaught ? "caught" : mouse.IsVisible ? "visible" : "hidden")}" +
                      $"{(mouse != null ? $" at ({mouse.transform.position.x:0.00}, {mouse.transform.position.z:0.00})" : "")} " +
                      $"{BirdState()} fps {1f / Mathf.Max(0.001f, Time.smoothDeltaTime):0}");
        }

        string BirdState()
        {
            if (birds == null || birds.Length == 0) return "birds none";
            float nearest = float.MaxValue;
            int down = 0;
            foreach (var b in birds)
            {
                if (b.OnGround) down++;
                nearest = Mathf.Min(nearest, Vector3.Distance(b.transform.position, cat.transform.position));
            }
            string hog = hedgehog == null ? "" : $" hedgehog ({hedgehog.transform.position.x:0.0}, {hedgehog.transform.position.z:0.0})" +
                (hedgehog.AtNest ? " at nest" : hedgehog.Trusts ? " leads" : hedgehog.IsCurled ? " curled" : "");
            return $"birds {down}/{birds.Length} on ground, nearest {nearest:0.0} m{hog}";
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
                    float[] v = Array.ConvertAll(xz, n => float.TryParse(n, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : 0f);
                    if (v.Length >= 2)
                    {
                        Quaternion turn = v.Length >= 3 ? Quaternion.Euler(0f, v[2], 0f) : cat.transform.rotation;
                        float y = v.Length >= 4 ? v[3] : 0f; // the 4th value: height, to start on a roof
                        cat.Teleport(new Vector3(v[0], y + 0.05f, v[1]), turn);
                    }
                }
            }
        }
    }
}
