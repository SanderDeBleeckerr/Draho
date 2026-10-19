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
        /// <summary>True in the free-roam epilogue after THE END (the gifts for the humans appear).</summary>
        public static bool InEpilogue { get; private set; }

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
            public Func<Vector3?> Target; // where the goal compass points (null: no compass)
        }

        Beat[] beats;
        CatMotor cat;
        SlidingDoor door;
        Bird[] birds;
        Climbable maple;
        Hedgehog hedgehog;
        bool visitedLindens;
        Duck[] ducks;
        Climbable[] lindens;
        Transform churchDoor, bakery;
        NpcCat greyCat;
        StoryItem toyMouse, jingleBall;
        Transform gardenLamp;
        Fox fox;
        Climbable courtPipe;
        Transform frontChimney, northChimney, roofHatch, catTree;
        Humans humans;
        Fisherman fisherman;
        Catnip catnip;
        float greetTime;
        Pigeon[] pigeons;
        Pigeon gull;
        StoryItem croissant, breakfast;

        static bool InApartment(Vector3 p) => p.x > 0.1f && p.x < 7.6f && p.z > 0f && p.z < 5f && p.y < 1.5f;
        static Vector3? P(float x, float y, float z) => new Vector3(x, y, z);
        static bool InBegijnhof(Vector3 p) => p.z < -112.4f && p.x > -35f && p.y < 2f;

        Vector3? NearestBird()
        {
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (var b in birds)
            {
                if (b == null || !b.OnGround) continue;
                float d = Vector3.Distance(b.transform.position, cat.transform.position);
                if (d < bestD) { bestD = d; best = b.transform.position; }
            }
            return best;
        }

        Vector3? NearestLinden()
        {
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (var l in lindens)
            {
                float d = Vector3.Distance(l.transform.position, cat.transform.position);
                if (d < bestD) { bestD = d; best = l.transform.position; }
            }
            return best;
        }

        /// <summary>The next step on the way home from the far side: quay, shed pipe, riverhouse pipe, ivy, garden door.</summary>
        Vector3? HomeWaypoint()
        {
            Vector3 p = cat.transform.position;
            if (p.x < -47f) return P(-41.5f, 0f, -93f);                          // across the footbridge
            if (InBegijnhof(p)) return P(-30.5f, 0f, -114.6f);                   // the shed by the wall
            if (p.x < -31f && p.y < 1f) return P(-33.9f, 0f, -9.5f);             // the pipe on the quay shed
            if (p.x < -31f && p.y < 4.5f) return P(-31.5f, 3.2f, -9.5f);         // the pipe on the riverhouse
            if (p.y > 4.5f) return P(-22f, 6.5f, -0.4f);                         // the ivy down into the garden
            return P(2f, 0f, 2.5f);                                              // home
        }

        static bool InGarden(Vector3 p) => p.x < 0f && p.x > -24f && p.z > 0f && p.z < 5f && p.y < 3f;
        static bool InCourt(Vector3 p) => p.x > 4.6f && p.x < 7.6f && p.z > 3.2f && p.z < 5f && p.y < 4f;

        Vector3? NearestPigeon()
        {
            Vector3? best = null;
            float bestD = float.MaxValue;
            foreach (var b in pigeons)
            {
                if (b == null || !b.OnGround) continue;
                float d = Vector3.Distance(b.transform.position, cat.transform.position);
                if (d < bestD) { bestD = d; best = b.transform.position; }
            }
            return best;
        }

        static bool InFlat(Vector3 p) => p.x > -1.9f && p.x < 7.6f && p.z > 5.3f && p.z < 9f && p.y < 1.5f;
        Mouse bakeryMouse;

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
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
            InEpilogue = false;
            cat = FindAnyObjectByType<CatMotor>();
            door = FindAnyObjectByType<SlidingDoor>();
            birds = FindObjectsByType<Bird>(FindObjectsSortMode.None);
            hedgehog = FindAnyObjectByType<Hedgehog>();
            ducks = FindObjectsByType<Duck>(FindObjectsSortMode.None);
            lindens = Array.FindAll(FindObjectsByType<Climbable>(FindObjectsSortMode.None), c => c.name.StartsWith("linden"));
            churchDoor = GameObject.Find("Marker_ChurchDoor")?.transform;
            bakery = GameObject.Find("Marker_Bakery")?.transform;
            greyCat = FindAnyObjectByType<NpcCat>();
            var items = FindObjectsByType<StoryItem>(FindObjectsSortMode.None);
            toyMouse = Array.Find(items, i => i.name == "ToyMouse");
            jingleBall = Array.Find(items, i => i.name == "JingleBall");
            var lampGo = GameObject.Find("GardenLamp");
            if (lampGo != null) gardenLamp = lampGo.transform;
            fox = FindAnyObjectByType<Fox>();
            var pipeGo = GameObject.Find("court_pipe");
            if (pipeGo != null) courtPipe = pipeGo.GetComponent<Climbable>();
            var chimneyGo = GameObject.Find("chimney_front");
            if (chimneyGo != null) frontChimney = chimneyGo.transform;
            var northGo = GameObject.Find("chimney_north");
            if (northGo != null) northChimney = northGo.transform;
            var hatchGo = GameObject.Find("RoofHatch");
            if (hatchGo != null) roofHatch = hatchGo.transform;
            var treeGo = GameObject.Find("cat_tree");
            if (treeGo != null) catTree = treeGo.transform;
            humans = FindAnyObjectByType<Humans>();
            fisherman = FindAnyObjectByType<Fisherman>();
            catnip = FindAnyObjectByType<Catnip>();
            var birdsOfStone = FindObjectsByType<Pigeon>(FindObjectsSortMode.None);
            pigeons = Array.FindAll(birdsOfStone, p => p.name.StartsWith("Pigeon_"));
            gull = Array.Find(birdsOfStone, p => p.name == "Gull");
            var allItems = FindObjectsByType<StoryItem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            croissant = Array.Find(allItems, i => i.name == "Croissant");
            breakfast = Array.Find(allItems, i => i.name == "Breakfast");
            bakeryMouse = Array.Find(FindObjectsByType<Mouse>(FindObjectsInactive.Include, FindObjectsSortMode.None), m => m.name == "BakeryMouse");
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
                    Target = () => P(7.2f, 0f, 1.05f),
                    Begin = () => { if (mouse != null) mouse.gameObject.SetActive(true); },
                    Done = () => mouse == null || mouse.IsCaught,
                },
                new Beat
                {
                    Story = "Got it! Drahomil, mouse hunter. The bounty board by the kitchen pays for every mouse in the neighbourhood.",
                    Goal = "Sit in front of the garden door until it opens.",
                    Target = () => door != null ? door.transform.position : (Vector3?)null,
                    Done = () => door == null || door.IsOpen,
                },
                new Beat
                {
                    Story = "The door slides open. Fresh air, birds, and new smells.",
                    Goal = "Go out into the garden.",
                    Target = () => P(-3f, 0f, 2.5f),
                    Done = () => cat.transform.position.x < gardenEdgeX - 0.4f,
                },
                new Beat
                {
                    Story = "Blackbirds hop on the lawn. They look for worms, and they watch for cats.",
                    Goal = "Sneak up on a blackbird: switch DUCK on and creep close.",
                    Target = () => NearestBird(),
                    Done = () => birds.Length == 0 || Array.Exists(birds, b => b.WasStalked),
                },
                new Beat
                {
                    Story = "Almost! The blackbird escapes to the maple by the fence. It sings at Drahomil from a branch.",
                    Goal = "Climb the maple: walk up to the trunk and hold the middle of the stick.",
                    Target = () => P(-22f, 0f, 3.9f),
                    Begin = SendBirdToMaple,
                    Done = () => maple == null || (cat.CurrentClimbable == maple && cat.transform.position.y > 1.2f),
                },
                new Beat
                {
                    Story = "The trunk is too thin higher up. But at the end of the garden, ivy grows up the old wall. A ladder for cats.",
                    Goal = "Walk up to the ivy, then hold your thumb in the middle of the stick to climb.",
                    Target = () => P(-22f, 0f, 0.5f),
                    Done = () => cat.IsClimbing && cat.transform.position.y > 2.6f,
                },
                new Beat
                {
                    Story = "End of chapter 1! But the ivy does not stop here. It goes all the way up to the roof.",
                    Goal = "Keep holding: climb to the top and pull yourself onto the roof.",
                    Target = () => P(-22f, 6.5f, -0.4f),
                    Done = () => cat.IsGrounded && cat.transform.position.y > 6f,
                },
                new Beat
                {
                    Story = "Chapter 2: The Rooftops. Warm gravel, chimneys, and the whole block below. Nobody comes up here. Except cats... and butterflies: two dance over a pot of red geraniums.",
                    Goal = "Catch a butterfly. They zigzag, and they escape when you walk at them: sneak close (DUCK), then jump when one flutters low.",
                    Target = () => P(-13.5f, 6.9f, -5f),
                    Done = () => Moth.ButterfliesCaught >= 1,
                },
                new Beat
                {
                    Story = "Got one! It tickles your nose... and you let it go. Over the roofs, on the high wall, a drainpipe glints in the sun.",
                    Goal = "Find the drainpipe on the high wall. Climb it to the highest roof.",
                    Target = () => P(-7f, 5.8f, -4.2f),
                    Done = () => cat.IsGrounded && cat.transform.position.y > 8.4f,
                },
                new Beat
                {
                    Story = "The highest point of the block. Drahomil can see the garden, the maple and the blackbirds, all very small.",
                    Goal = "Find a way down to the low roof at the back.",
                    Target = () => P(-6f, 3.7f, -14f),
                    Done = () => cat.IsGrounded && cat.transform.position.y > 3.5f && cat.transform.position.y < 4f
                                 && cat.transform.position.z < -11f,
                },
                new Beat
                {
                    Story = "From up here Drahomil sees the neighbour's garden: wild and empty for months.",
                    Goal = "Go back down to your garden. Find a loose plank in the fence near the maple.",
                    Target = () => cat.transform.position.y > 3f && cat.transform.position.z < -11f ? P(-10.6f, 3.7f, -16f) : cat.transform.position.y > 5f ? P(-22f, 6.5f, -0.4f) : P(-19.5f, 0f, 4.6f),
                    Done = () => cat.transform.position.z > 5.3f && cat.transform.position.y < 1f,
                },
                new Beat
                {
                    Story = "Chapter 3: Over the Fence. The grass is as high as a cat. Something snuffles in it.",
                    Goal = "Find what is snuffling in the tall grass.",
                    Target = () => hedgehog != null ? hedgehog.transform.position : (Vector3?)null,
                    Done = () => hedgehog == null || Vector3.Distance(hedgehog.transform.position, cat.transform.position) < 1.2f,
                },
                new Beat
                {
                    Story = "A hedgehog! It rolls into a prickly ball. Hedgehogs need time to trust a cat.",
                    Goal = "Sit still close to the hedgehog. Do not move.",
                    Target = () => hedgehog != null ? hedgehog.transform.position : (Vector3?)null,
                    Done = () => hedgehog == null || hedgehog.Trusts,
                },
                new Beat
                {
                    Story = "The hedgehog sniffs Drahomil's nose and trots off. It wants to show something.",
                    Goal = "Follow the hedgehog. Go slowly.",
                    Target = () => hedgehog != null ? hedgehog.transform.position : (Vector3?)null,
                    Done = () => hedgehog == null || (hedgehog.AtNest
                                 && Vector3.Distance(hedgehog.transform.position, cat.transform.position) < 2.5f),
                },
                new Beat
                {
                    Story = "Under the old wheelbarrow: a nest with three tiny hoglets. Drahomil keeps the secret.",
                    Goal = "Back up on the roofs, go west, past the end of the garden. Something glitters there.",
                    Target = () => cat.transform.position.y < 5f ? P(-22f, 0f, 0.5f) : P(-27f, 6.5f, -8f),
                    Done = () => cat.transform.position.x < -26f && cat.transform.position.y > 5f,
                },
                new Beat
                {
                    Story = "Chapter 4: The Riverside. Water! Boats, ducks and old houses along the quay. Far down the river: a white wall.",
                    Goal = "Get down to the quay: the shed by the water is a good step.",
                    Target = () => cat.transform.position.y > 4f ? P(-32.2f, 3.2f, -9.5f) : P(-33f, 0f, -12f),
                    Done = () => cat.IsGrounded && cat.transform.position.y < 0.5f && cat.transform.position.x < -31f,
                },
                new Beat
                {
                    Story = "Ducks on the river! They watch Drahomil: a friend, or a fox? Running scares them away.",
                    Goal = "Sit still at the edge of the quay until a duck paddles close.",
                    Target = () => P(-34.7f, 0f, -20f),
                    Done = () => ducks.Length == 0 || Array.Exists(ducks, d => d.IsClose),
                },
                new Beat
                {
                    Story = "Down the quay an old fisherman sits on a stool and watches his float. Beside him: a bucket of silver fish!",
                    Goal = "Take a fish from the bucket. Move only while he watches his float: when he hums, freeze!",
                    Target = () => fisherman != null && !fisherman.FishTaken ? P(-33.8f, 0.3f, -27.3f) : (Vector3?)null,
                    Done = () => fisherman == null || fisherman.FishTaken,
                },
                new Beat
                {
                    Story = "The fisherman laughs: \"Clever cat. Keep it, I have plenty.\" The quay becomes a narrow edge along a house, right above the water. Cats do not like swimming.",
                    Goal = "Follow the river to the white wall. Walk on the edge, or take the rooftops.",
                    Target = () => P(-33f, 0f, -111.4f),
                    Done = () => cat.transform.position.z < -104f,
                },
                new Beat
                {
                    Story = "Chapter 5: The Begijnhof. Behind this old wall live quiet people and very old trees. The gate is shut, but ivy grows up the wall.",
                    Goal = "Climb the ivy over the begijnhof wall.",
                    Target = () => P(-33f, 0f, -111.4f),
                    Done = () => cat.transform.position.z < -112.6f && cat.IsGrounded,
                },
                new Beat
                {
                    Story = "A big quiet lawn, rows of old lindens, little houses with red doors and a church. Only the bells make a sound.",
                    Goal = "Explore: sit under a linden, then find the big church.",
                    Target = () => visitedLindens ? (churchDoor != null ? churchDoor.position : (Vector3?)null) : NearestLinden(),
                    Begin = () => { visitedLindens = false; },
                    Done = () =>
                    {
                        Vector3 p = cat.transform.position;
                        if (!visitedLindens && Array.Exists(lindens, l => Vector3.Distance(Flat(l.transform.position), Flat(p)) < 2f))
                        {
                            visitedLindens = true;
                            Debug.Log("STORY sat under a linden");
                        }
                        return visitedLindens && (churchDoor == null || Vector3.Distance(Flat(churchDoor.position), Flat(p)) < 4f);
                    },
                },
                new Beat
                {
                    Story = "Someone watches from the church step: an old grey cat. This is her begijnhof.",
                    Goal = "Walk slowly to the grey cat. Do not run: she does not like that.",
                    Target = () => greyCat != null ? greyCat.transform.position : (Vector3?)null,
                    Done = () => greyCat == null || greyCat.Welcomed,
                },
                new Beat
                {
                    Story = "She blinks slowly: you may stay. Then she lifts her nose towards the gardens behind the church. A green, wild smell drifts on the air...",
                    Goal = "Follow the green scent trail to where it comes from.",
                    Begin = () => catnip?.ShowTrail(true),
                    Target = () => catnip != null ? catnip.transform.position : (Vector3?)null,
                    Done = () => catnip == null || catnip.Sniffed,
                },
                new Beat
                {
                    Story = "CATNIP! You sniff, rub, roll and race round the lawn like a kitten. When the zoomies fade, the grey cat sniffs the air again. Across the river: fresh bread!",
                    Goal = "Leave over the wall (rain barrel, shed, wall), cross the footbridge and find the bakery.",
                    Target = () => InBegijnhof(cat.transform.position) ? P(-30.5f, 0f, -114.6f) : (bakery != null ? bakery.position : (Vector3?)null),
                    Done = () => bakery == null || Vector3.Distance(Flat(bakery.position), Flat(cat.transform.position)) < 5f,
                },
                new Beat
                {
                    Story = "Chapter 6: The Bakery. The baker sweeps flour and mutters about a mouse in the bread crates. A job for a mouse hunter!",
                    Goal = "Catch the bakery mouse. Use DUCK to sneak up on it.",
                    Target = () => P(-57.4f, 0f, -99.8f),
                    Done = () => bakeryMouse == null || bakeryMouse.IsCaught,
                },
                new Beat
                {
                    Story = "Got it! But a big herring gull lands on the bread crates and snatches a croissant. The baker shakes his broom at it.",
                    Goal = "Chase the gull: sneak close from the side (DUCK) and touch it. Gulls flee from a cat that comes straight at them.",
                    Target = () => gull != null ? gull.transform.position : (Vector3?)null,
                    Done = () => gull == null || gull.WasTouched,
                },
                new Beat
                {
                    Story = "SKREEE! The gull flies up to the roof, screaming, and drops the croissant by the door.",
                    Goal = "Eat the croissant crumbs by the bakery door.",
                    Begin = () => { if (croissant != null) croissant.gameObject.SetActive(true); },
                    Target = () => croissant != null && croissant.gameObject.activeSelf ? croissant.transform.position : (Vector3?)null,
                    Done = () => croissant == null || croissant.Taken,
                },
                new Beat
                {
                    Story = "The baker smiles and puts a saucer of milk by the door. Drahomil, mouse hunter and gull chaser of the riverside. Time to go home.",
                    Goal = "Go home: quay, drainpipes up to the roofs, the ivy down into the garden.",
                    Target = () => HomeWaypoint(),
                    Done = () => InApartment(cat.transform.position),
                },
                new Beat
                {
                    Story = "Chapter 7: The Neighbour's Flat. Home... but the toy mouse is gone! Corgi paw prints lead to the neighbour's open window.",
                    Goal = "Go through the loose plank and the wild garden. Jump in at the open window.",
                    Target = () => cat.transform.position.z < 5f && cat.transform.position.x < 0f ? P(-19.5f, 0f, 4.6f) : P(-2.6f, 0f, 7.1f),
                    Done = () => InFlat(cat.transform.position),
                },
                new Beat
                {
                    Story = "Hrozna, the corgi next door, sleeps in her basket. Right by her nose: Drahomil's toy mouse! And all over the floor: her rubber toys.",
                    Goal = "Sneak to the basket (DUCK) and take the toy mouse back. Do not step on a rubber toy: they squeak, and Hrozna wakes up.",
                    Target = () => toyMouse != null && toyMouse.gameObject.activeSelf ? toyMouse.transform.position : (Vector3?)null,
                    Done = () => toyMouse == null || toyMouse.Taken,
                },
                new Beat
                {
                    Story = "Got it! Hrozna opens one eye, barks once, and goes back to sleep. Not enemies, just loud neighbours.",
                    Goal = "Bring the toy mouse home: out of the window, through the wild garden and the loose plank.",
                    Target = () => InFlat(cat.transform.position) ? P(-2.6f, 0f, 7.1f) : cat.transform.position.z > 5f ? P(-19.5f, 0f, 4.6f) : P(2f, 0f, 2.5f),
                    Done = () => InApartment(cat.transform.position),
                },
                new Beat
                {
                    Story = "Chapter 8: The Inner Court. Roo-coo... roo-coo... Pigeons have landed in the little court behind the glass!",
                    Goal = "The court door next to the cat tree is open a crack. Squeeze through.",
                    Target = () => P(4.6f, 0f, 3.8f),
                    Done = () => InCourt(cat.transform.position),
                },
                new Beat
                {
                    Story = "Three fat pigeons peck between the round stones. Pigeons watch each other: when one flies, they all fly.",
                    Goal = "Sneak up (DUCK) and touch a pigeon. Do not go straight at it: walk past it from the side.",
                    Target = () => NearestPigeon(),
                    Done = () => Pigeon.AnyTouched,
                },
                new Beat
                {
                    Story = "Flap-flap-flap! The flock flies up the light well. Something shines behind the drainpipe: your old jingle ball, lost last spring!",
                    Goal = "Climb the drainpipe in the court (hold the middle of the stick) and take the jingle ball.",
                    Target = () => jingleBall != null && jingleBall.gameObject.activeSelf ? jingleBall.transform.position : (Vector3?)null,
                    Done = () => jingleBall == null || jingleBall.Taken,
                },
                new Beat
                {
                    Story = "Ting-a-ling! The pigeons coo from the gutter, very offended. The sky turns orange: the evening comes.",
                    Goal = "Go home and out into the garden: the evening air smells of cut grass.",
                    Begin = () => SkyMood.Set(SkyMood.Mood.Evening, 12f),
                    Target = () => new Vector3(-1f, 0f, 2.9f),
                    Done = () => InGarden(cat.transform.position),
                },
                // ---- Chapter 9: Night ----
                new Beat
                {
                    Story = "Chapter 9: Night. The humans are still not home. The moon comes up over the fence, and the lamp by the door switches on. Soft grey wings flutter around it: moths!",
                    Goal = "Go to the lamp by the sliding door.",
                    Begin = () => SkyMood.Set(SkyMood.Mood.Night, 10f),
                    Target = () => gardenLamp != null ? gardenLamp.position : (Vector3?)null,
                    Done = () => gardenLamp != null && Vector3.Distance(cat.transform.position, gardenLamp.position - Vector3.up * 1.9f) < 2.2f,
                },
                new Beat
                {
                    Story = "Moths do not fly into the light: they keep their backs to it, so they loop and loop around the lamp. Sometimes one drops low.",
                    Goal = "Catch 3 moths: jump when one drops low. The garden table is a good place to jump from.",
                    Target = () => gardenLamp != null ? gardenLamp.position : (Vector3?)null,
                    Done = () => Moth.CaughtCount >= 3,
                },
                new Beat
                {
                    Story = "Crunchy and dusty! Three moths. The night garden is full of small sounds now... A twig snaps behind the fence.",
                    Goal = "Listen... something moves in the neighbour's wild garden. Go and look over the fence.",
                    Target = () => new Vector3(-4f, 0f, 5f),
                    Done = () => beatTime > 5f && (cat.transform.position.z > 4.2f || cat.transform.position.y > 1.2f || fox == null),
                },
                new Beat
                {
                    Story = "A shadow with a white-tipped tail: a fox! Foxes and cats share the night, and most nights they leave each other alone. This one walks its route. Where does it go?",
                    Goal = "Follow the fox through the wild garden. Stay low (DUCK) and keep a few metres back: too close, and it stops and stares.",
                    Begin = () => fox?.Begin(),
                    Target = () => fox != null && !fox.AtDen ? fox.transform.position : (Vector3?)null,
                    Done = () => fox == null || fox.AtDen,
                },
                new Beat
                {
                    Story = "The fox slips into its den under the bushes. Behind the den, the old fence has a broken plank: a dark gap that smells of water.",
                    Goal = "Squeeze through the gap in the fence behind the fox's den.",
                    Begin = () => fox?.SkipToDen(),
                    Target = () => new Vector3(-26f, 0f, 6.3f),
                    Done = () => cat.transform.position.x < -26.3f,
                },
                new Beat
                {
                    Story = "The river bank! The water whispers in the dark, and an old lamp-post hums on the quay. Home is close: ivy grows up the back of the shed at the end of your own garden.",
                    Goal = "Climb the ivy on the shed (hold the middle of the stick) and get onto its roof.",
                    Target = () => new Vector3(-27f, 2f, 5.1f),
                    Done = () => cat.transform.position.y > 3.9f && cat.transform.position.x > -28.2f && cat.transform.position.x < -23.8f && cat.transform.position.z < 5.05f,
                },
                new Beat
                {
                    Story = "From the shed roof you see your own garden, silver in the moonlight, and the warm lamp by your door.",
                    Goal = "Jump down into your garden and go home to the lamp by the door.",
                    Target = () => gardenLamp != null ? gardenLamp.position : (Vector3?)null,
                    Done = () => InGarden(cat.transform.position) && gardenLamp != null && Vector3.Distance(cat.transform.position, gardenLamp.position - Vector3.up * 1.9f) < 2.5f,
                },
                new Beat
                {
                    Story = "Home again, by the warm lamp. The moths are still dancing. The humans are not back yet... End of chapter 9.",
                    Goal = "",
                    Done = () => beatTime > 7f,
                },
                // ---- Chapter 10: The high roofs ----
                new Beat
                {
                    Story = "Chapter 10: The high roofs. You cannot sleep. The pigeons coo up in the light well, and the moon shines on the roof of your own house. From up there you can see the whole town.",
                    Goal = "Go to the inner court and climb the drainpipe all the way up to the roof (hold the middle of the stick).",
                    // The bracket that held the jingle ball is loose now: the pipe goes on to the gutter.
                    Begin = () => courtPipe?.Configure(8.8f, true),
                    Target = () => new Vector3(7.5f, 1f, 4.15f),
                    Done = () => cat.transform.position.y > 8.5f,
                },
                new Beat
                {
                    Story = "The roof! Chimneys stand like trees in a forest of slate, and a hundred windows glow over the dark street. The air smells of rain.",
                    Goal = "Walk up the slate roof to the big chimney on the ridge.",
                    Target = () => frontChimney != null ? frontChimney.position + Vector3.up * 2.2f : (Vector3?)null,
                    Done = () => frontChimney != null && cat.transform.position.y > 10.5f && Vector3.Distance(Flat(cat.transform.position), Flat(frontChimney.position)) < 2.2f,
                },
                new Beat
                {
                    Story = "From the ridge you see the river, the begijnhof tower and all your roads of today. The wind turns: far away, thunder grumbles.",
                    Goal = "",
                    Done = () => beatTime > 6f,
                },
                new Beat
                {
                    Story = "The storm breaks! Rain drums on the slate, lightning flashes, and the wind pulls at your fur.",
                    Goal = "Go along the ridge to the neighbour's chimney. When the wind howls, DUCK: low cats do not blow away.",
                    Begin = () => Storm.Begin(),
                    Target = () => northChimney != null ? northChimney.position + Vector3.up * 1.9f : (Vector3?)null,
                    Done = () => northChimney != null && cat.transform.position.y > 10f && Vector3.Distance(Flat(cat.transform.position), Flat(northChimney.position)) < 2f,
                },
                new Beat
                {
                    Story = "Behind the big chimney you are out of the wind. Lightning! In the flash you see a dark square on the flat roof behind the light well: a hatch, open a crack.",
                    Goal = "Get to the hatch on the flat roof (back over the ridge, past the light well) and slip inside.",
                    Target = () => roofHatch != null ? roofHatch.position : (Vector3?)null,
                    Done = () => roofHatch != null && Vector3.Distance(cat.transform.position, roofHatch.position) < 0.8f,
                },
                // ---- Chapter 11: Home ----
                new Beat
                {
                    Story = "Chapter 11: Home. Through the hatch, down a dusty attic ladder and the dark stairs, and in through your own cat flap. Home, and wet to the bone.",
                    Goal = "Dry off: climb into your basket on top of the cat tree.",
                    Begin = () =>
                    {
                        Storm.End();
                        if (index < 0) return; // replayed at the start of a later saved game or test: no fade, no jump home
                        int at = index;
                        ScreenFade.Run(() =>
                        {
                            if (index != at) return;
                            SkyMood.Set(SkyMood.Mood.Night, 0.1f);
                            cat.Teleport(new Vector3(3.4f, 0.05f, 3.4f), Quaternion.Euler(0f, 60f, 0f));
                        });
                    },
                    Target = () => catTree != null ? catTree.position + Vector3.up * 0.8f : (Vector3?)null,
                    Done = () => catTree != null && cat.transform.position.y > 0.5f && Vector3.Distance(Flat(cat.transform.position), Flat(catTree.position)) < 0.6f,
                },
                new Beat
                {
                    Story = "Warm and dry, you sleep... Birds sing: morning! The storm has washed the town clean. Then: keys in the door. The humans are home!",
                    Goal = "Run to the front door in the kitchen and greet them: rub against their legs.",
                    Begin = () => { SkyMood.Set(SkyMood.Mood.Dawn, 10f); humans?.ComeHome(); greetTime = 0f; },
                    Target = () => humans != null ? humans.transform.GetChild(0).position + Vector3.up * 0.4f : (Vector3?)null,
                    Done = () =>
                    {
                        if (humans == null) return true;
                        bool close = false;
                        foreach (Transform person in humans.transform)
                            if (Vector3.Distance(Flat(cat.transform.position), Flat(person.position)) < 0.55f) close = true;
                        greetTime = close ? greetTime + Time.deltaTime : Mathf.Max(0f, greetTime - Time.deltaTime);
                        return greetTime > 2.5f;
                    },
                },
                new Beat
                {
                    Story = "Purrrr. They smell of trains and coffee. The sun climbs over the roofs, and something rattles in the kitchen: the food box!",
                    Goal = "Breakfast! Go to your bowl between the counter and the island.",
                    Begin = () => { if (breakfast != null) breakfast.gameObject.SetActive(true); }, // the golden morning light stays: a patch of sun for the ending
                    Target = () => breakfast != null && breakfast.gameObject.activeSelf ? breakfast.transform.position : (Vector3?)null,
                    Done = () => breakfast == null || breakfast.Taken,
                },
                new Beat
                {
                    Story = "Full and warm, you curl up in a patch of morning sun. One of your humans kneels down and scratches you behind the ear: \"Did you have a quiet day, Drahomil?\" You blink, very slowly. THE END. Thank you for playing!",
                    Goal = "",
                    Done = () => beatTime > 14f,
                },
                // ---- Epilogue: free roam (the story is over; the town is still there) ----
                new Beat
                {
                    Story = "Epilogue: a new day. Your humans are home, the cat flap is open and the whole town is yours again.",
                    Goal = "Free roam: bring your humans a gift from every corner of town (6), find every feather and nap spot, fill the bounty board.",
                    Begin = () =>
                    {
                        humans?.SettleIn();
                        InEpilogue = true;
                        SkyMood.Set(SkyMood.Mood.Day, 20f);
                        var p = Draho.Core.Progress.Instance;
                        if (p != null)
                            Debug.Log($"STORY epilogue: feathers {p.CollectedCount}/{p.TotalCollectables}, naps {p.NapCount}");
                    },
                    Done = () => false,
                },
            };

            bool urlBeat = ApplyUrlArguments(out int startBeat);
            // Continue a saved game (unless the URL picks a beat, as the playtest does).
            var progress = Draho.Core.Progress.Instance;
            if (!urlBeat && progress != null && progress.SavedBeat > 0)
            {
                startBeat = progress.SavedBeat;
                if (progress.TryGetSavedPosition(out Vector3 saved, out float savedYaw))
                    cat.Teleport(saved + Vector3.up * 0.05f, Quaternion.Euler(0f, savedYaw, 0f));
                Debug.Log($"SAVE resume at beat {startBeat}");
            }
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
            Draho.Core.Progress.Instance?.BeatReached(index);
            Debug.Log($"STORY beat {index}: {beat.Story}");
            GoalCompass.Target = beat.Target;
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

        /// <summary>Test hook: SendMessage('StoryDirector', 'TestTeleport', 'x,z,yaw[,y]').</summary>
        public void TestTeleport(string args)
        {
            float[] v = Array.ConvertAll(args.Split(','), n => float.Parse(n, System.Globalization.CultureInfo.InvariantCulture));
            cat.Teleport(new Vector3(v[0], (v.Length >= 4 ? v[3] : 0f) + 0.05f, v[1]), Quaternion.Euler(0f, v.Length >= 3 ? v[2] : 0f, 0f));
        }

        bool ApplyUrlArguments(out int startBeat)
        {
            startBeat = 0;
            bool beatGiven = false;
            string url = Application.absoluteURL;
            int q = string.IsNullOrEmpty(url) ? -1 : url.IndexOf('?');
            if (q < 0) return false;
            foreach (string pair in url.Substring(q + 1).Split('&'))
            {
                string[] kv = pair.Split('=');
                if (kv.Length != 2) continue;
                if (kv[0] == "beat") beatGiven = int.TryParse(kv[1], out startBeat);
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
            return beatGiven;
        }
    }
}
