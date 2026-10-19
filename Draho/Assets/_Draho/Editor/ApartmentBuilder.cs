using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Draho.Story;
using Draho.UI;
using Draho.World;
using Object = UnityEngine.Object;

namespace Draho.EditorTools
{
    /// <summary>
    /// Builds the apartment level from Levels/Apartment/apartment_layout.json (the blueprint, see
    /// docs/APARTMENT.md). Walls, glass and floors are simple boxes; furniture is Kenney Furniture Kit
    /// models, each scaled to the exact size in the blueprint. Called by PlaygroundBuilder.Build().
    /// </summary>
    public static class ApartmentBuilder
    {
        const string Root = "Assets/_Draho";
        const string LayoutPath = Root + "/Levels/Apartment/apartment_layout.json";
        const string KenneyDir = Root + "/ThirdParty/Kenney/FurnitureKit";
        const string SlidingDoorFbx = Root + "/Art/Props/SlidingDoor.fbx";
        public const string ScenePath = Root + "/Scenes/Apartment.unity";

        // The cat's CharacterController is 0.22 m wide. The real door is open a crack; in the game
        // the gap must still let the capsule through.
        const float MinDoorGap = 0.26f;
        const float ModuleWidth = 0.6f; // one kitchen cabinet

        static Transform level;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        // ------------------------------------------------------------------ JSON shape
#pragma warning disable 0649 // fields are set by JsonUtility
        [Serializable] class Layout
        {
            public string name;
            public FloorInfo floor;
            public WallInfo wall;
            public Room[] rooms;
            public Opening[] openings;
            public Prop[] props;
            public Court[] courts;
            public Edge[] garden_edges;
            public Spawn spawn;
            public Hole[] mouse_holes;
            public Lamp[] lamps;
            public ClimbableInfo[] climbables;
            public Zone[] garden_zones;
            public BirdSpot[] birds;
            public GardenPath[] garden_paths;
            public Building[] buildings;
            public Prop[] roof_props;
            public Edge[] bounds;
            public ContextBuilding[] context_buildings;
            public WildGarden wild_garden;
            public Ground[] grounds;
            public QuayLampInfo quay_lamp;
            public Water water;
            public Begijnhof begijnhof;
            public DuckSpot[] ducks;
            public Ground[] bridges;
            public Bakery bakery;
            public MouseTerritory[] mouse_territories;
            public Feather[] feathers;
            public float[] bounty_board; // x, y, z, yaw of the cork board
        }
        [Serializable] class MouseTerritory { public string id, name; public int bounty; public Hole[] holes; }
        [Serializable] class Feather { public string id; public float x, y, z; }
        [Serializable] class Bakery
        {
            public float x, z0, z1;
            public string awning_color;
            public Hole[] mouse_holes;
            public Spot[] crates, bins;
            public float[] marker;
        }
        [Serializable] class BegCat { public float x, z, yaw, step; }
        [Serializable] class BegExit { public BegBarrel barrel; }
        [Serializable] class BegBarrel { public float x, z, r, h; }
        [Serializable] class Begijnhof
        {
            public BegHouse[] houses;
            public Seg[] walls, blocks;
            public BegTree[] lindens, trees;
            public float[] church_door;
            public BegCat cat;
            public BegExit exit;
        }
        [Serializable] class BegHouse { public float x, z, w, d, yaw, h, ridge; public string front; public bool church; }
        [Serializable] class BegTree { public float x, z, h; public string kind; }
        [Serializable] class Seg { public float x0, z0, x1, z1; }
        [Serializable] class DuckSpot { public float x, z; public int count; }
        [Serializable] class WildGarden
        {
            public float x0, x1, z0, z1;
            public string color;
            public int grass_clumps;
            public Spot[] bushes;
            public Spot wheelbarrow, chair, barrel, hedgehog;
        }
        [Serializable] class Spot { public float x, z, r, yaw; }
        [Serializable] class ContextBuilding { public float x, z, w, d, yaw, h, y0; public string faces; }
        [Serializable] class Building
        {
            public string id, color, roof_color, material, roof, front; // material: brick | plaster; roof: flat | pitched
            public float x0, x1, z0, z1, h, ridge;
            public float base_y;    // walls start here (upper floors over the apartment)
            public string windows;  // faces with window panes, for example "-x+z"
            public string ridge_along; // pitched: "x" or "z" (default: along the long side)
        }
        [Serializable] class QuayLampInfo { public float x, z, h; }
        [Serializable] class Ground { public string id, color, material; public float x0, x1, z0, z1, top; }
        [Serializable] class Water { public string color; public float x0, x1, z0, z1, top; }
        [Serializable] class GardenPath { public string id, color; public float[] from, to; public float w, slab; }
        [Serializable] class BirdSpot { public string id; public float x, z; }
        [Serializable] class Zone { public string id, material, color; public float x0, x1; }
        [Serializable] class FloorInfo { public float tile_size; public string color; }
        [Serializable] class WallInfo { public float height; public float thickness; public string color; }
        [Serializable] class Room { public string id; public float x0, z0, x1, z1; public bool outdoor; }
        [Serializable] class Opening { public string id, wall, model; public float from, to, height; }
        [Serializable] class Prop
        {
            public string id, label, kenney, color, top_color, facing;
            public float x, z, w, d, h, y;
        }
        [Serializable] class Court
        {
            public string id, color;
            public float x0, z0, x1, z1, glass_height, shaft_height; // shaft_height: the house walls round the open court
            public string[] glass, outer_walls;
            public Door[] doors;
        }
        [Serializable] class Door { public string id, side; public float from, to, open_gap; }
        [Serializable] class Edge
        {
            public string id, color, style, post_color, material;
            public float[] from, to;
            public float h, post_spacing;
            public float y0; // bounds: the invisible wall starts at this height (roof edges)
            public float gap_at, gap_w, gap_h; // a hole at the bottom of a fence (a loose plank)
        }
        [Serializable] class Spawn { public float x, z; public string facing; }
        [Serializable] class Hole { public string id, facing; public float x, y, z; }
        [Serializable] class ClimbableInfo
        {
            public string id, type, color, top_color, wall; // wall: where the wall is, seen from a pipe ("-z" default, "-x")
            public float x, z, w, d, h, radius, climb_top, bottom;
            public bool standable;
        }
        [Serializable] class Lamp { public string id, color; public float x, y, z, intensity, range; }
#pragma warning restore 0649

        // ------------------------------------------------------------------ build
        public static void Build(GameObject catPrefab)
        {
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            materials.Clear();
            lowSphere = null;

            Scene scene = PlaygroundBuilder.NewLitScene(Quaternion.Euler(45f, 240f, 0f)); // real afternoon sun (see garden_note)
            level = new GameObject("Apartment").transform;

            Rect inside = InsideRect(layout);
            BuildFloors(layout, inside);
            BuildOuterWalls(layout, inside);
            foreach (var court in layout.courts ?? new Court[0]) BuildCourt(layout, court);
            foreach (var prop in layout.props) BuildProp(prop);
            BuildGarden(layout);
            BuildPaths(layout);
            BuildBuildings(layout);
            BuildWildGarden(layout.wild_garden);
            BuildBegijnhof(layout.begijnhof);
            BuildDucks(layout);
            BuildBridges(layout);
            BuildBakery(layout.bakery);
            foreach (var c in layout.climbables ?? new ClimbableInfo[0]) BuildClimbable(c);

            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(level, false);
            spawn.SetPositionAndRotation(new Vector3(layout.spawn.x, 0.02f, layout.spawn.z),
                Quaternion.Euler(0f, FacingYaw(layout.spawn.facing) + 180f, 0f)); // the cat looks along +Z
            Transform hud = PlaygroundBuilder.AddPlayerRig(scene, catPrefab, spawn, new Color(0.62f, 0.72f, 0.85f));
            BuildLamps(layout);
            BuildStory(layout, hud);

            OptimiseScene();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Draho: built '{layout.name}' with {layout.props.Length} props -> {ScenePath}");
        }

        /// <summary>
        /// Performance (mobile WebGL): everything that never moves is marked static (Unity combines it into
        /// fewer draw calls), small details cast no shadow and go on the Detail layer (not drawn far away),
        /// and fog hides the end of the camera's view.
        /// </summary>
        static void OptimiseScene()
        {
            var dynamicTypes = new[]
            {
                typeof(Mouse), typeof(Bird), typeof(Pigeon), typeof(Duck), typeof(Hedgehog), typeof(NpcCat), typeof(Fatsocat), typeof(Tortoise),
                typeof(Hrozna), typeof(Collectable), typeof(StoryItem), typeof(SlidingDoor), typeof(Animator), typeof(Moth), typeof(NightLamp), typeof(Fox), typeof(Humans), typeof(Fisherman), typeof(SqueakyToy), typeof(SunBeam), typeof(Catnip), typeof(WaterGlint), typeof(NapSpot), typeof(WellRested), typeof(WetWeather), typeof(LampCone), typeof(GlowWorms), typeof(CloudShadows), typeof(Gift), typeof(Viewpoint), typeof(ShootingStars), typeof(LightningBolt), typeof(MorningMist), typeof(ScratchSpot), typeof(DayCycle), typeof(RainSplashes), typeof(ChimneySmoke), typeof(TvGlow), typeof(LaserDot), typeof(ParticleSystem),
            };
            bool IsDynamic(Transform t)
            {
                for (var p = t; p != null; p = p.parent)
                    foreach (var type in dynamicTypes)
                        if (p.GetComponent(type) != null) return true;
                return false;
            }
            int statics = 0, details = 0, noShadow = 0;
            var combine = new List<GameObject>();
            foreach (var t in level.GetComponentsInChildren<Transform>(true))
            {
                if (!IsDynamic(t))
                {
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                    statics++;
                    if (t.GetComponent<MeshRenderer>() != null && t.gameObject.activeInHierarchy) combine.Add(t.gameObject);
                }
                var r = t.GetComponent<Renderer>();
                if (r == null) continue;
                Vector3 size = r.bounds.size;
                float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                if (largest < 0.5f)
                {
                    if (r.shadowCastingMode != ShadowCastingMode.Off) noShadow++;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    if (t.gameObject.layer == 0 && t.GetComponent<Collider>() == null)
                    {
                        t.gameObject.layer = Draho.CameraRig.AdaptiveQuality.DetailLayer;
                        details++;
                    }
                }
                if (t.name == "TallGrass" || t.name == "WildGrass") r.shadowCastingMode = ShadowCastingMode.Off;
            }
            // The project's static-batching setting is not in the repository, so combine at load time.
            level.gameObject.AddComponent<Draho.Core.StaticScenery>().Configure(combine.ToArray());
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.84f, 0.9f);
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 170f;
            Debug.Log($"Draho: optimised scene: {statics} static objects, {details} details (culled beyond 35 m), {noShadow} small shadows off");
        }

        static Rect InsideRect(Layout layout)
        {
            var rooms = layout.rooms.Where(r => !r.outdoor).ToArray();
            float x0 = rooms.Min(r => r.x0), x1 = rooms.Max(r => r.x1);
            float z0 = rooms.Min(r => r.z0), z1 = rooms.Max(r => r.z1);
            return Rect.MinMaxRect(x0, z0, x1, z1);
        }

        // ------------------------------------------------------------------ floors and walls
        static void BuildFloors(Layout layout, Rect inside)
        {
            var tiles = Mat("Apt_FloorTiles", Hex(layout.floor.color), 0.35f,
                TileTexture(), new Vector2(inside.width, inside.height) / layout.floor.tile_size);
            Box("Floor", new Vector3(inside.center.x, -0.05f, inside.center.y),
                new Vector3(inside.width + 2 * layout.wall.thickness, 0.1f, inside.height + 2 * layout.wall.thickness), tiles);
        }

        static void BuildOuterWalls(Layout layout, Rect inside)
        {
            float t = layout.wall.thickness, h = layout.wall.height;
            var wall = Mat("Apt_Wall", Hex(layout.wall.color), 0.1f);
            var sides = new (string side, Vector2 a, Vector2 b)[]
            {
                ("south", new Vector2(inside.xMin - t, inside.yMin - t / 2), new Vector2(inside.xMax + t, inside.yMin - t / 2)),
                ("north", new Vector2(inside.xMin - t, inside.yMax + t / 2), new Vector2(inside.xMax + t, inside.yMax + t / 2)),
                ("west", new Vector2(inside.xMin - t / 2, inside.yMin), new Vector2(inside.xMin - t / 2, inside.yMax)),
                ("east", new Vector2(inside.xMax + t / 2, inside.yMin), new Vector2(inside.xMax + t / 2, inside.yMax)),
            };
            foreach (var (side, a, b) in sides)
            {
                bool alongX = Mathf.Approximately(a.y, b.y);
                float start = alongX ? a.x : a.y, end = alongX ? b.x : b.y;
                var cuts = (layout.openings ?? new Opening[0]).Where(o => o.wall == side).OrderBy(o => o.from).ToList();
                float cursor = start;
                foreach (var o in cuts)
                {
                    WallSegment($"Wall_{side}", alongX, alongX ? a.y : a.x, cursor, o.from, 0f, h, t, wall);
                    WallSegment($"Wall_{side}_above_{o.id}", alongX, alongX ? a.y : a.x, o.from, o.to, o.height, h, t, wall);
                    if (o.model == "SlidingDoor")
                        SlidingDoorAt(o, alongX, alongX ? a.y : a.x, side);
                    else
                        GlassPanel($"Glass_{o.id}", alongX, alongX ? a.y : a.x, o.from, o.to, o.height);
                    cursor = o.to;
                }
                WallSegment($"Wall_{side}", alongX, alongX ? a.y : a.x, cursor, end, 0f, h, t, wall);
            }
        }

        /// <summary>A wall box along X (fixed z = line) or along Z (fixed x = line), from..to, between y0 and y1.</summary>
        static void WallSegment(string name, bool alongX, float line, float from, float to, float y0, float y1,
            float thickness, Material mat)
        {
            float len = to - from;
            if (len <= 0.001f || y1 - y0 <= 0.001f) return;
            float mid = (from + to) / 2, y = (y0 + y1) / 2;
            var pos = alongX ? new Vector3(mid, y, line) : new Vector3(line, y, mid);
            var size = alongX ? new Vector3(len, y1 - y0, thickness) : new Vector3(thickness, y1 - y0, len);
            Box(name, pos, size, mat);
        }

        static void GlassPanel(string name, bool alongX, float line, float from, float to, float height)
        {
            var frame = Mat("Apt_Frame", new Color(0.18f, 0.19f, 0.2f), 0.4f);
            WallSegment(name, alongX, line, from, to, 0.02f, height - 0.04f, 0.02f, GlassMat());
            WallSegment(name + "_sill", alongX, line, from, to, 0f, 0.02f, 0.06f, frame);
            WallSegment(name + "_head", alongX, line, from, to, height - 0.04f, height, 0.06f, frame);
        }

        /// <summary>
        /// The Sketchfab sliding door (Tools/Blender/convert_sliding_door.py), fitted into the opening.
        /// Its "Slider" child moves sideways behind the fixed pane when the cat waits in front of it.
        /// </summary>
        static void SlidingDoorAt(Opening o, bool alongX, float line, string side)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(SlidingDoorFbx);
            if (asset == null)
            {
                Debug.LogWarning($"Draho: {SlidingDoorFbx} not found; using a fixed glass panel.");
                GlassPanel($"Glass_{o.id}", alongX, line, o.from, o.to, o.height);
                return;
            }
            float width = o.to - o.from, mid = (o.from + o.to) / 2;
            var root = new GameObject($"SlidingDoor_{o.id}").transform;
            root.SetParent(level, false);
            root.position = alongX ? new Vector3(mid, 0f, line) : new Vector3(line, 0f, mid);

            var door = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            door.transform.localPosition = Vector3.zero;
            door.transform.localRotation = Quaternion.identity;
            door.transform.localScale = Vector3.one;
            Bounds b = WorldBounds(door);
            // Turn the door so its width runs along the wall.
            bool doorAlongX = b.size.x >= b.size.z;
            if (doorAlongX != alongX) door.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            b = WorldBounds(door);
            float doorWidth = alongX ? b.size.x : b.size.z;
            float s = width / doorWidth;
            door.transform.localScale = new Vector3(s, o.height / b.size.y, s);
            b = WorldBounds(door);
            door.transform.position += root.position - new Vector3(b.center.x, b.min.y, b.center.z);

            foreach (var r in door.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m =>
                    m != null && m.name.Contains("Glass") ? GlassMat() : Mat("Apt_DoorFrame", new Color(0.93f, 0.93f, 0.92f), 0.4f)).ToArray();

            Transform slider = door.GetComponentsInChildren<Transform>().First(t => t.name == "Slider");
            Transform fixedGlass = door.GetComponentsInChildren<Transform>().First(t => t.name == "FixedGlass");
            Bounds sb = WorldBounds(slider.gameObject), fb = WorldBounds(fixedGlass.gameObject);

            // Colliders: the sliding panel moves with its collider; the fixed half has its own box.
            var sliderCol = slider.gameObject.AddComponent<BoxCollider>();
            sliderCol.center = slider.InverseTransformPoint(sb.center);
            sliderCol.size = Abs(slider.InverseTransformVector(sb.size));
            var body = slider.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            var fixedCol = new GameObject("FixedPaneCollider").AddComponent<BoxCollider>();
            fixedCol.transform.SetParent(root, false);
            fixedCol.transform.position = fb.center;
            fixedCol.size = new Vector3(Mathf.Max(fb.size.x, 0.12f), b.size.y, Mathf.Max(fb.size.z, 0.12f));

            // Open: the slider moves over the fixed pane.
            Vector3 along = alongX ? Vector3.right : Vector3.forward;
            float direction = Mathf.Sign(Vector3.Dot(fb.center - sb.center, along));
            float travel = (alongX ? sb.size.x : sb.size.z) * 0.92f;

            // Waiting areas in front of the door, on both sides, including the doorway itself.
            Vector3 inward = side == "west" ? Vector3.right : side == "east" ? Vector3.left
                : side == "south" ? Vector3.forward : Vector3.back;
            var zones = new[] { 1f, -1f }.Select(sign =>
            {
                Vector3 c = root.position + inward * sign * 0.35f + Vector3.up * 0.5f;
                Vector3 size = alongX ? new Vector3(width, 1f, 0.9f) : new Vector3(0.9f, 1f, width);
                return new Bounds(c, size);
            }).ToArray();
            root.gameObject.AddComponent<SlidingDoor>().Configure(slider, along * direction * travel, zones);
        }

        static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

        // ------------------------------------------------------------------ inner court
        static void BuildCourt(Layout layout, Court c)
        {
            float h = c.glass_height > 0 ? c.glass_height : layout.wall.height;
            var pebbles = Mat("Apt_Pebbles", Hex(c.color), 0.2f, PebbleTexture(), new Vector2(c.x1 - c.x0, c.z1 - c.z0) / 0.9f); // cobbles about 11 cm
            Box("Court_Stones", new Vector3((c.x0 + c.x1) / 2, 0.005f, (c.z0 + c.z1) / 2),
                new Vector3(c.x1 - c.x0, 0.01f, c.z1 - c.z0), pebbles, collider: false);

            var frame = Mat("Apt_Frame", new Color(0.18f, 0.19f, 0.2f), 0.4f);
            foreach (string side in c.glass ?? new string[0])
            {
                bool alongX = side == "south" || side == "north";
                float line = side == "south" ? c.z0 : side == "north" ? c.z1 : side == "west" ? c.x0 : c.x1;
                float start = alongX ? c.x0 : c.z0, end = alongX ? c.x1 : c.z1;
                var doors = (c.doors ?? new Door[0]).Where(d => d.side == side).OrderBy(d => d.from).ToList();
                float cursor = start;
                foreach (var d in doors)
                {
                    GlassPanel($"CourtGlass_{side}", alongX, line, cursor, d.from, h);
                    // The door panel, pushed open a crack at its far end.
                    float gap = Mathf.Max(d.open_gap, MinDoorGap);
                    GlassPanel($"CourtDoor_{d.id}", alongX, line + 0.04f, d.from, d.to - gap, h);
                    WallSegment($"CourtDoor_{d.id}_post", alongX, line, d.from - 0.03f, d.from, 0f, h, 0.06f, frame);
                    cursor = d.to;
                }
                GlassPanel($"CourtGlass_{side}", alongX, line, cursor, end, h);
            }
            // Corner posts where the glass walls meet.
            WallSegment("CourtPost", true, c.z0, c.x0 - 0.03f, c.x0 + 0.03f, 0f, h, 0.06f, frame);
            if (c.shaft_height > h) CourtShaft(layout, c, h);
        }

        /// <summary>
        /// The court is open to the sky, but the upper floors of the house rise round it: a light well.
        /// Plaster walls up to the roof, windows with sills (pigeon perches) and a zinc gutter at the top.
        /// </summary>
        static void CourtShaft(Layout layout, Court c, float glassTop)
        {
            float t = layout.wall.thickness, top = c.shaft_height;
            var plaster = Mat("Apt_CourtPlaster", new Color(0.85f, 0.83f, 0.79f), 0.08f, PlasterTexture(), new Vector2(1f, 3f));
            // North and east: the outer walls of the house (the apartment wall below is 2.6 m high).
            float baseNE = layout.wall.height;
            WallSegment("Shaft_north", true, c.z1 + t / 2, c.x0 - t, c.x1 + t, baseNE, top, t, plaster);
            WallSegment("Shaft_east", false, c.x1 + t / 2, c.z0 - t, c.z1, baseNE, top, t, plaster);
            // South and west: the house above the glass walls.
            WallSegment("Shaft_south", true, c.z0 - t / 2, c.x0 - t, c.x1 + t, glassTop, top, t, plaster);
            WallSegment("Shaft_west", false, c.x0 - t / 2, c.z0, c.z1, glassTop, top, t, plaster);

            var window = Mat("Apt_CourtWindow", new Color(0.16f, 0.2f, 0.24f), 0.85f);
            var stone = Mat("Apt_Coping", Color.gray, 0.2f);
            float midX = (c.x0 + c.x1) / 2, midZ = (c.z0 + c.z1) / 2;
            foreach (float floorY in new[] { 3.3f, 6.1f })
            {
                // A window on the north wall and a smaller one on the west wall, each with a stone sill.
                Box("ShaftWindow", new Vector3(midX, floorY + 0.7f, c.z1 - 0.005f), new Vector3(1.3f, 1.3f, 0.01f), window, collider: false);
                Box("ShaftSill", new Vector3(midX, floorY - 0.025f, c.z1 - 0.1f), new Vector3(1.5f, 0.05f, 0.2f), stone, collider: false);
                Box("ShaftWindow", new Vector3(c.x0 + 0.005f, floorY + 0.7f, midZ), new Vector3(0.01f, 1.3f, 0.9f), window, collider: false);
                Box("ShaftSill", new Vector3(c.x0 + 0.1f, floorY - 0.025f, midZ), new Vector3(0.2f, 0.05f, 1.1f), stone, collider: false);
            }
            var zinc = Mat("Apt_Zinc", new Color(0.55f, 0.57f, 0.58f), 0.55f);
            Box("ShaftGutter", new Vector3(midX, top - 0.06f, c.z1 - 0.08f), new Vector3(c.x1 - c.x0, 0.12f, 0.16f), zinc, collider: false);
            Box("ShaftGutter", new Vector3(c.x1 - 0.08f, top - 0.06f, midZ), new Vector3(0.16f, 0.12f, c.z1 - c.z0), zinc, collider: false);
        }

        /// <summary>
        /// Chapter 8 in the inner court: three pigeons on the stones (they perch on the sills and the gutter)
        /// and Drahomil's old jingle ball, stuck behind a bracket of the court drainpipe.
        /// </summary>
        /// <summary>
        /// Chapter 9: a wall lamp next to the sliding door (on from the evening) and moths that loop around it.
        /// The lantern hangs 1 m from the garden table, so the table is a good place to jump from.
        /// </summary>
        static void BuildGardenNight()
        {
            var iron = Mat("Lamp_Iron", new Color(0.12f, 0.12f, 0.13f), 0.4f);
            var glass = Mat("Lamp_Glow", new Color(1f, 0.85f, 0.6f), 0.6f);
            glass.EnableKeyword("_EMISSION");
            glass.SetColor("_EmissionColor", new Color(0.15f, 0.11f, 0.06f));
            EditorUtility.SetDirty(glass);
            Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (type == PrimitiveType.Sphere) go.GetComponent<MeshFilter>().sharedMesh = LowSphere();
                return go.transform;
            }

            var root = new GameObject("GardenLamp");
            root.transform.SetParent(level, false);
            root.transform.position = new Vector3(-0.05f, 1.95f, 1.0f);
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.01f, 0f, 0f), new Vector3(0.02f, 0.16f, 0.1f), iron);   // wall plate
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.09f, 0.05f, 0f), new Vector3(0.16f, 0.02f, 0.02f), iron); // arm
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.17f, 0.03f, 0f), new Vector3(0.12f, 0.02f, 0.12f), iron); // roof
            var bulb = Part(PrimitiveType.Cube, root.transform, new Vector3(-0.17f, -0.05f, 0f), new Vector3(0.09f, 0.14f, 0.09f), glass);
            Part(PrimitiveType.Cube, root.transform, new Vector3(-0.17f, -0.13f, 0f), new Vector3(0.11f, 0.02f, 0.11f), iron); // base
            var light = new GameObject("GardenLampLight").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = new Vector3(-0.3f, -0.08f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.74f, 0.45f);
            light.range = 6f;
            light.intensity = 0f;
            light.enabled = false;
            light.shadows = LightShadows.None;
            root.AddComponent<NightLamp>().Configure(light, bulb.GetComponent<Renderer>());
            AddLampCone(root.transform, root.transform.position + new Vector3(-0.17f, -0.14f, 0f), 1.8f, 0.9f, light);

            // Four moths: pale wings, a furry brown body, about 4 cm long. +Z is forward.
            var body = Mat("Moth_Body", new Color(0.42f, 0.34f, 0.26f), 0.1f);
            var wing = Mat("Moth_Wing", new Color(0.8f, 0.74f, 0.62f), 0.1f);
            Vector3 centre = new Vector3(-0.75f, 1.8f, 1.0f);
            for (int i = 0; i < 4; i++)
            {
                var moth = new GameObject($"Moth_{i + 1}");
                moth.transform.SetParent(level, false);
                moth.transform.position = centre + new Vector3(0.3f * Mathf.Cos(i * 1.6f), 0f, 0.3f * Mathf.Sin(i * 1.6f));
                Part(PrimitiveType.Sphere, moth.transform, Vector3.zero, new Vector3(0.014f, 0.014f, 0.035f), body);
                Transform Wing(float side)
                {
                    var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
                    pivot.SetParent(moth.transform, false);
                    pivot.localPosition = new Vector3(side * 0.005f, 0.004f, 0f);
                    Part(PrimitiveType.Cube, pivot, new Vector3(side * 0.02f, 0f, -0.002f), new Vector3(0.04f, 0.002f, 0.03f), wing);
                    return pivot;
                }
                var left = Wing(-1f);
                var right = Wing(1f);
                moth.AddComponent<Moth>().Configure(centre, left, right);
            }
        }

        /// <summary>
        /// Chapter 9: a red fox (about 90 cm with the tail) that walks a night route west through the wild
        /// garden, from the workshop end to a den under the bushes in the far corner. +Z is forward.
        /// </summary>
        /// <summary>Chapter 9: an old cast-iron lamp-post at the north end of the quay; on at night.</summary>
        static Light BuildQuayLamp(QuayLampInfo q, string name = "QuayLamp")
        {
            if (q == null || q.h <= 0f) return null;
            var iron = Mat("QuayLamp_Iron", new Color(0.13f, 0.17f, 0.15f), 0.35f); // old dark green paint
            var glass = Mat("Lamp_Glow", new Color(1f, 0.85f, 0.6f), 0.6f);
            var root = new GameObject(name);
            root.transform.SetParent(level, false);
            root.transform.position = new Vector3(q.x, 0f, q.z);
            Box("Foot", root.transform.position + Vector3.up * 0.2f, new Vector3(0.26f, 0.4f, 0.26f), iron).transform.SetParent(root.transform, true);
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(root.transform, false);
            post.transform.localPosition = Vector3.up * (q.h / 2);
            post.transform.localScale = new Vector3(0.1f, q.h / 2, 0.1f);
            post.GetComponent<Renderer>().sharedMaterial = iron;
            Box("Cap", root.transform.position + Vector3.up * (q.h + 0.42f), new Vector3(0.4f, 0.05f, 0.4f), iron, collider: false).transform.SetParent(root.transform, true);
            var bulb = Box("Lantern", root.transform.position + Vector3.up * (q.h + 0.22f), new Vector3(0.28f, 0.36f, 0.28f), glass, collider: false);
            bulb.transform.SetParent(root.transform, true);
            var light = new GameObject("QuayLampLight").AddComponent<Light>();
            light.transform.SetParent(root.transform, false);
            light.transform.localPosition = Vector3.up * (q.h - 0.1f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.range = 9f;
            light.intensity = 0f;
            light.enabled = false;
            light.shadows = LightShadows.None;
            var lamp = root.AddComponent<NightLamp>();
            lamp.Configure(light, bulb.GetComponent<Renderer>());
            var so = new SerializedObject(lamp);
            so.FindProperty("intensity").floatValue = 4.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            AddLampCone(root.transform, root.transform.position + Vector3.up * (q.h + 0.03f), q.h, 1.7f, light);
            return light;
        }

        /// <summary>
        /// Three old lamp-posts along the far bank of the river, each with a glitter path on the water towards the
        /// viewer at night (WaterGlint; additive, the sunbeam's material).
        /// </summary>
        static void BuildFarBankLamps(Layout layout)
        {
            if (layout.water == null) return;
            var mat = AdditiveMaterial();
            float edge = layout.water.x0; // the far bank starts where the water ends (west side)
            int i = 0;
            foreach (float z in new[] { 8f, -6f, -22f })
            {
                var light = BuildQuayLamp(new QuayLampInfo { x = edge - 0.6f, z = z, h = 4.2f }, $"FarBankLamp_{++i}");
                if (light == null || mat == null) continue;
                var glint = new GameObject($"Glint_{i}");
                glint.transform.SetParent(level, false);
                glint.transform.position = new Vector3(edge + 0.05f, layout.water.top + 0.015f, z);
                glint.AddComponent<MeshFilter>();
                var r = glint.AddComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                glint.AddComponent<WaterGlint>().Configure(light);
            }
        }

        /// <summary>
        /// Lamp-posts along the street in front of the house (east of the facade), seen from the roofs at night: the
        /// quay lamp's post, light and cone.
        /// </summary>
        static void BuildStreetLamps()
        {
            int i = 0;
            foreach (float z in new[] { -4f, 6f, 16f, 26f })
                BuildQuayLamp(new QuayLampInfo { x = 21.6f, z = z, h = 4.5f }, $"StreetLamp_{++i}");
        }

        /// <summary>
        /// Windows on the scenery blocks: flat panes in rows (one floor = 3 m), about a third of them lit at
        /// night. All panes are in two meshes (lit and dark), so the whole block costs two draw calls.
        /// </summary>
        static void BuildWindows(ContextBuilding[] blocks)
        {
            var rng = new System.Random(1931);
            var lit = new List<Vector3>();
            var dark = new List<Vector3>();
            var litN = new List<Vector3>();
            var darkN = new List<Vector3>();
            const float paneW = 0.9f, paneH = 1.3f, step = 2.6f, floor = 3f;
            foreach (var c in blocks)
            {
                var q = Quaternion.Euler(0f, c.yaw, 0f);
                var centre = new Vector3(c.x, 0f, c.z);
                // Four faces: (normal, tangent, half depth, face width) in the block's own axes.
                var faces = new[]
                {
                    (Vector3.forward, Vector3.right, c.d / 2, c.w, "+z"), (Vector3.back, Vector3.left, c.d / 2, c.w, "-z"),
                    (Vector3.right, Vector3.back, c.w / 2, c.d, "+x"), (Vector3.left, Vector3.forward, c.w / 2, c.d, "-x"),
                };
                foreach (var (normal, tangent, half, width, name) in faces)
                {
                    if (!string.IsNullOrEmpty(c.faces) && !c.faces.Contains(name)) continue;
                    int count = Mathf.FloorToInt((width - 1f) / step);
                    if (count < 1) continue;
                    for (float y = Mathf.Max(1.2f, c.y0 + 0.7f); y + paneH < c.h - 0.5f; y += floor)
                        for (int i = 0; i < count; i++)
                        {
                            float t = (i - (count - 1) / 2f) * step;
                            Vector3 mid = centre + q * (normal * (half + 0.02f) + tangent * t) + Vector3.up * (y + paneH / 2);
                            Vector3 right = q * tangent * (paneW / 2), up = Vector3.up * (paneH / 2);
                            bool on = rng.NextDouble() < 0.35;
                            var list = on ? lit : dark;
                            list.Add(mid - right - up); list.Add(mid - right + up); list.Add(mid + right + up); list.Add(mid + right - up);
                            var nl = on ? litN : darkN;
                            for (int k = 0; k < 4; k++) nl.Add(q * normal);
                        }
                }
            }
            Mesh Panes(string name, List<Vector3> verts, List<Vector3> normals)
            {
                var mesh = new Mesh { name = name };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(normals);
                var tris = new int[verts.Count / 4 * 6];
                for (int i = 0, v = 0; v < verts.Count; v += 4, i += 6)
                {
                    // The tangent points to the viewer's left, so this order is clockwise seen from outside.
                    tris[i] = v; tris[i + 1] = v + 2; tris[i + 2] = v + 1;
                    tris[i + 3] = v; tris[i + 4] = v + 3; tris[i + 5] = v + 2;
                }
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                string path = $"{PlaygroundBuilder.ArtDir}/Meshes/{name}.asset";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            var glass = Mat("Window_Glass", new Color(0.1f, 0.12f, 0.16f), 0.85f);
            var warm = Mat("Window_Lit", new Color(0.13f, 0.13f, 0.15f), 0.8f);
            warm.EnableKeyword("_EMISSION");
            warm.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            warm.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(warm);
            GameObject PaneObject(string name, Mesh mesh, Material m)
            {
                var go = new GameObject(name);
                go.transform.SetParent(level, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                return go;
            }
            PaneObject("Windows_Dark", Panes("WindowsDark", dark, darkN), glass);
            PaneObject("Windows_Lit", Panes("WindowsLit", lit, litN), warm).AddComponent<WindowGlow>().Configure(warm);
            Debug.Log($"Draho: windows {lit.Count / 4} lit, {dark.Count / 4} dark");
        }

        /// <summary>
        /// Stars and the moon (Draho/NightSky): billboards far away around the camera, drawn only at night.
        /// Root objects (not under the level), so static batching does not touch their special vertices.
        /// </summary>
        static void BuildNightSky()
        {
            var shader = Shader.Find("Draho/NightSky");
            if (shader == null) { Debug.LogWarning("Draho/NightSky not found: no stars"); return; }
            Material SkyMat(string name, Color color, float moon)
            {
                string path = $"{PlaygroundBuilder.MaterialDir}/{name}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
                m.shader = shader;
                m.SetColor("_Color", color);
                m.SetFloat("_Moon", moon);
                EditorUtility.SetDirty(m);
                return m;
            }
            Mesh Quads(string name, List<(Vector3 dir, float size, Color tint)> items)
            {
                var verts = new List<Vector3>();
                var uvs = new List<Vector2>();
                var sizes = new List<Vector2>();
                var colors = new List<Color>();
                var tris = new List<int>();
                var rng = new System.Random(77);
                foreach (var (dir, size, tint) in items)
                {
                    int v = verts.Count;
                    float seed = (float)rng.NextDouble();
                    foreach (var uv in new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) })
                    {
                        verts.Add(dir);
                        uvs.Add(uv);
                        sizes.Add(new Vector2(size, 0f));
                        colors.Add(new Color(tint.r, tint.g, tint.b, seed));
                    }
                    tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 }); // Cull Off: the side does not matter
                }
                var mesh = new Mesh { name = name };
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetUVs(1, sizes);
                mesh.SetColors(colors);
                mesh.SetTriangles(tris, 0);
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4000f); // the shader moves it around the camera
                string path = $"{PlaygroundBuilder.ArtDir}/Meshes/{name}.asset";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            void Sky(string name, Mesh mesh, Material m)
            {
                var go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            // 700 stars in the upper sky, more small than big, a few warm and a few blue.
            var rngStars = new System.Random(2024);
            var stars = new List<(Vector3, float, Color)>();
            while (stars.Count < 700)
            {
                var d = new Vector3((float)rngStars.NextDouble() * 2f - 1f, (float)rngStars.NextDouble(), (float)rngStars.NextDouble() * 2f - 1f);
                if (d.sqrMagnitude > 1f || d.sqrMagnitude < 0.01f) continue;
                d.Normalize();
                if (d.y < 0.08f) continue; // the horizon is in the fog
                float r = (float)rngStars.NextDouble();
                float size = 0.6f + r * r * r * 1.7f;
                double hue = rngStars.NextDouble();
                var tint = hue < 0.12 ? new Color(1f, 0.85f, 0.7f) : hue > 0.85 ? new Color(0.75f, 0.85f, 1f) : Color.white;
                stars.Add((d, size, tint));
            }
            Sky("NightStars", Quads("NightStars", stars), SkyMat("NightSky_Stars", new Color(0.9f, 0.93f, 1f), 0f));
            Sky("NightMoon", Quads("NightMoon", new List<(Vector3, float, Color)> { (Vector3.up, 9f, Color.white) }),
                SkyMat("NightSky_Moon", new Color(0.95f, 0.95f, 0.88f), 1f));
        }

        /// <summary>
        /// Chapter 10's storm: rain round the camera (stretched soft particles, no shadows, at most 1200),
        /// switched on and driven by <see cref="Storm"/>. A root object: it follows the camera.
        /// </summary>
        static void BuildRain()
        {
            var go = new GameObject("Rain");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.035f);
            main.startColor = new Color(0.72f, 0.78f, 0.9f, 0.45f);
            main.maxParticles = 1200;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(16f, 16f, 0.1f);
            shape.rotation = new Vector3(90f, 0f, 0f); // emits along -Y: down
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.045f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var dust = AssetDatabase.LoadAssetAtPath<Material>($"{PlaygroundBuilder.MaterialDir}/Dust.mat");
            if (dust != null) renderer.sharedMaterial = dust;
            else Debug.LogWarning("Dust.mat not found: the rain has no material");
            go.AddComponent<Storm>().Configure(ps);
        }

        /// <summary>
        /// Chapter 10-11: a roof hatch on the flat roof (the way down after the storm), the apartment's front door
        /// on the kitchen's east wall, and the humans' legs in front of it (hidden until they come home).
        /// </summary>
        static void BuildHomecoming(Layout layout)
        {
            var wood = Mat("Hatch_Wood", new Color(0.3f, 0.24f, 0.18f), 0.15f);
            var dark = Mat("Hatch_Dark", new Color(0.01f, 0.01f, 0.012f), 0f);
            var hatch = new GameObject("RoofHatch").transform;
            hatch.SetParent(level, false);
            hatch.position = new Vector3(2.2f, 8.8f, 6.5f);
            foreach (float s in new[] { -1f, 1f })
            {
                Box("HatchFrame", hatch.position + new Vector3(s * 0.45f, 0.06f, 0f), new Vector3(0.1f, 0.12f, 1.1f), wood, collider: false).transform.SetParent(hatch, true);
                Box("HatchFrame", hatch.position + new Vector3(0f, 0.06f, s * 0.5f), new Vector3(1.0f, 0.12f, 0.1f), wood, collider: false).transform.SetParent(hatch, true);
            }
            Box("HatchHole", hatch.position + Vector3.up * 0.005f, new Vector3(0.8f, 0.01f, 0.9f), dark, collider: false).transform.SetParent(hatch, true);
            var lid = Box("HatchLid", hatch.position + new Vector3(0f, 0.2f, -0.5f), new Vector3(0.85f, 0.04f, 0.95f), wood, collider: false);
            lid.transform.rotation = Quaternion.Euler(-25f, 0f, 0f); // open a crack, lifted by the storm
            lid.transform.SetParent(hatch, true);

            // The front door: on the kitchen's east wall, between the fridge and the island.
            float t = layout.wall.thickness, wallX = 7.6f - t / 2;
            var doorMat = Mat("Apt_FrontDoor", new Color(0.36f, 0.27f, 0.2f), 0.25f);
            var frame = Mat("Apt_DoorFrame", new Color(0.88f, 0.87f, 0.84f), 0.3f);
            Box("FrontDoor", new Vector3(wallX - 0.015f, 1.0f, 1.0f), new Vector3(0.03f, 2.0f, 0.8f), doorMat, collider: false);
            Box("FrontDoorFrame", new Vector3(wallX - 0.02f, 2.04f, 1.0f), new Vector3(0.04f, 0.08f, 0.96f), frame, collider: false);
            foreach (float s in new[] { -1f, 1f })
                Box("FrontDoorFrame", new Vector3(wallX - 0.02f, 1.0f, 1.0f + s * 0.44f), new Vector3(0.04f, 2.08f, 0.08f), frame, collider: false);
            Box("DoorHandle", new Vector3(wallX - 0.05f, 1.0f, 0.7f), new Vector3(0.04f, 0.03f, 0.12f), Mat("Apt_Metal", new Color(0.55f, 0.56f, 0.58f), 0.6f), collider: false);

            // The humans, seen from cat height: legs, shoes and the hem of a coat. +Z of each person faces the room.
            var humans = new GameObject("Humans").transform;
            humans.SetParent(level, false);
            var jeans = Mat("Human_Jeans", new Color(0.18f, 0.24f, 0.36f), 0.2f);
            var trousers = Mat("Human_Trousers", new Color(0.22f, 0.2f, 0.19f), 0.2f);
            var coat = Mat("Human_Coat", new Color(0.55f, 0.42f, 0.3f), 0.2f);
            var coat2 = Mat("Human_Coat2", new Color(0.2f, 0.3f, 0.26f), 0.2f);
            var shoe = Mat("Human_Shoe", new Color(0.08f, 0.07f, 0.07f), 0.4f);
            var people = new[] { (new Vector3(7.05f, 0f, 0.85f), jeans, coat), (new Vector3(6.85f, 0f, 1.3f), trousers, coat2) };
            foreach (var (pos, legs, top) in people)
            {
                var person = new GameObject("Person").transform;
                person.SetParent(humans, false);
                person.position = pos;
                person.rotation = Quaternion.Euler(0f, -90f, 0f); // facing into the kitchen (-x)
                foreach (float s in new[] { -1f, 1f })
                {
                    var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.DestroyImmediate(leg.GetComponent<Collider>());
                    leg.transform.SetParent(person, false);
                    leg.transform.localPosition = new Vector3(s * 0.1f, 0.45f, 0f);
                    leg.transform.localScale = new Vector3(0.13f, 0.42f, 0.13f);
                    leg.GetComponent<Renderer>().sharedMaterial = legs;
                    var foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(foot.GetComponent<Collider>());
                    foot.transform.SetParent(person, false);
                    foot.transform.localPosition = new Vector3(s * 0.1f, 0.04f, 0.05f);
                    foot.transform.localScale = new Vector3(0.11f, 0.08f, 0.28f);
                    foot.GetComponent<Renderer>().sharedMaterial = shoe;
                }
                var hem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(hem.GetComponent<Collider>());
                hem.transform.SetParent(person, false);
                hem.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                hem.transform.localScale = new Vector3(0.46f, 0.2f, 0.32f);
                hem.GetComponent<Renderer>().sharedMaterial = top;
            }
            humans.gameObject.AddComponent<Humans>();

            // Breakfast: a blue bowl between the back counter and the island (hidden until the humans fill it).
            var bowl = new GameObject("Breakfast");
            bowl.transform.SetParent(level, false);
            bowl.transform.position = new Vector3(5.3f, 0f, 1.0f);
            var dish = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(dish.GetComponent<Collider>());
            dish.transform.SetParent(bowl.transform, false);
            dish.transform.localPosition = Vector3.up * 0.03f;
            dish.transform.localScale = new Vector3(0.18f, 0.03f, 0.18f);
            dish.GetComponent<Renderer>().sharedMaterial = Mat("Bowl_Blue", new Color(0.25f, 0.42f, 0.7f), 0.7f);
            var food = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(food.GetComponent<Collider>());
            food.transform.SetParent(bowl.transform, false);
            food.transform.localPosition = Vector3.up * 0.06f;
            food.transform.localScale = new Vector3(0.13f, 0.04f, 0.13f);
            food.GetComponent<Renderer>().sharedMaterial = Mat("Cat_Food", new Color(0.5f, 0.32f, 0.2f), 0.4f);
            food.GetComponent<MeshFilter>().sharedMesh = LowSphere();
            bowl.AddComponent<StoryItem>().Configure("Breakfast! Crunch, crunch, purr.", 0.5f);
            bowl.SetActive(false);

            // The humans' suitcase, open on the living-room floor in the morning sun (hidden until they unpack).
            // Low walls with colliders: the cat steps or hops in. +X is the long side.
            var suitcase = new GameObject("Suitcase").transform;
            suitcase.SetParent(level, false);
            suitcase.SetPositionAndRotation(new Vector3(1.4f, 0f, 1.3f), Quaternion.Euler(0f, 20f, 0f));
            var shell = Mat("Suitcase_Shell", new Color(0.55f, 0.12f, 0.1f), 0.5f);
            var lining = Mat("Suitcase_Lining", new Color(0.82f, 0.76f, 0.62f), 0.1f);
            void Panel(string name, Vector3 local, Vector3 size, Material m, Vector3 euler = default, bool solid = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(suitcase, false);
                go.transform.localPosition = local;
                go.transform.localEulerAngles = euler;
                go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = m;
            }
            Panel("Bottom", new Vector3(0f, 0.015f, 0f), new Vector3(0.72f, 0.03f, 0.46f), lining);
            Panel("SideFront", new Vector3(0f, 0.08f, -0.22f), new Vector3(0.72f, 0.16f, 0.025f), shell);
            Panel("SideBack", new Vector3(0f, 0.08f, 0.22f), new Vector3(0.72f, 0.16f, 0.025f), shell);
            Panel("SideLeft", new Vector3(-0.35f, 0.08f, 0f), new Vector3(0.025f, 0.16f, 0.46f), shell);
            Panel("SideRight", new Vector3(0.35f, 0.08f, 0f), new Vector3(0.025f, 0.16f, 0.46f), shell);
            // The lid lies open, flat on the floor behind (a standing lid walled off the camera).
            Panel("Lid", new Vector3(0f, 0.015f, 0.46f), new Vector3(0.72f, 0.03f, 0.46f), shell, solid: false);
            Panel("LidLining", new Vector3(0f, 0.032f, 0.46f), new Vector3(0.66f, 0.005f, 0.4f), lining, solid: false);
            Panel("Jumper", new Vector3(0.18f, 0.05f, 0.05f), new Vector3(0.22f, 0.04f, 0.2f), Mat("Suitcase_Jumper", new Color(0.3f, 0.45f, 0.35f), 0.05f), new Vector3(0f, 12f, 0f), solid: false);
            suitcase.gameObject.SetActive(false);

            // The TV on a low stand by the front wall, facing the sofa (TvGlow: on at night in the epilogue).
            var tv = new GameObject("TV").transform;
            tv.SetParent(level, false);
            tv.position = new Vector3(3.4f, 0f, 0.3f);
            var tvDark = Mat("TV_Case", new Color(0.06f, 0.06f, 0.07f), 0.6f);
            Box("TV_Stand", tv.position + new Vector3(0f, 0.2f, 0f), new Vector3(1.0f, 0.4f, 0.38f), Mat("TV_Stand", new Color(0.35f, 0.26f, 0.18f), 0.3f)).transform.SetParent(tv, true);
            Box("TV_Case", tv.position + new Vector3(0f, 0.72f, 0f), new Vector3(0.9f, 0.52f, 0.05f), tvDark, collider: false).transform.SetParent(tv, true);
            var screenMat = Mat("TV_Screen", new Color(0.02f, 0.02f, 0.03f), 0.9f);
            screenMat.EnableKeyword("_EMISSION");
            screenMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            screenMat.SetColor("_EmissionColor", Color.black);
            var tvScreen = Box("TV_Screen", tv.position + new Vector3(0f, 0.72f, 0.027f), new Vector3(0.84f, 0.46f, 0.005f), screenMat, collider: false);
            tvScreen.transform.SetParent(tv, true);
            var tvLight = new GameObject("TV_Light").AddComponent<Light>();
            tvLight.transform.SetParent(tv, false);
            tvLight.transform.localPosition = new Vector3(0f, 0.75f, 0.6f);
            tvLight.type = LightType.Point;
            tvLight.range = 4.5f;
            tvLight.intensity = 0f;
            tvLight.enabled = false;
            tvLight.shadows = LightShadows.None;
            tv.gameObject.AddComponent<TvGlow>().Configure(tvLight, tvScreen.GetComponent<Renderer>());

            // The laser game (LaserDot): a small bright red disc on the floor and a tiny red light.
            var laser = new GameObject("LaserDot").transform;
            laser.SetParent(level, false);
            var dotMat = Mat("Laser_Dot", new Color(1f, 0.05f, 0.05f), 0.9f);
            dotMat.EnableKeyword("_EMISSION");
            dotMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            dotMat.SetColor("_EmissionColor", new Color(3f, 0.1f, 0.1f));
            var dotGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(dotGo.GetComponent<Collider>());
            dotGo.name = "Dot";
            dotGo.transform.SetParent(laser, false);
            dotGo.transform.localScale = new Vector3(0.035f, 0.001f, 0.035f);
            dotGo.GetComponent<Renderer>().sharedMaterial = dotMat;
            dotGo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var dotLight = new GameObject("DotLight").AddComponent<Light>();
            dotLight.transform.SetParent(dotGo.transform, false);
            dotLight.transform.localPosition = Vector3.up * 40f; // 4 cm above the floor (the dot is scaled flat)
            dotLight.type = LightType.Point;
            dotLight.color = new Color(1f, 0.1f, 0.1f);
            dotLight.range = 0.4f;
            dotLight.intensity = 0.8f;
            dotLight.shadows = LightShadows.None;
            laser.gameObject.AddComponent<LaserDot>().Configure(dotGo.transform, dotLight);
        }

        /// <summary>
        /// Chapter 6: a herring gull on the far bank by the bakery, with a stolen croissant. It is a bird of the
        /// Pigeon kind (flees a straight approach, perches high) in its own group, bigger and warier; when the
        /// cat touches it, it drops the croissant (a StoryItem by the bakery door, hidden until then).
        /// </summary>
        static void BuildGull(Layout layout)
        {
            var b = layout.bakery;
            if (b == null) return;
            var white = Mat("Gull_White", new Color(0.93f, 0.93f, 0.92f), 0.25f);
            var grey = Mat("Gull_Grey", new Color(0.62f, 0.66f, 0.7f), 0.25f);
            var black = Mat("Bird_Black", new Color(0.04f, 0.04f, 0.045f), 0.45f);
            var yellow = Mat("Gull_Beak", new Color(0.98f, 0.8f, 0.2f), 0.4f);
            var pink = Mat("Pigeon_Feet", new Color(0.85f, 0.42f, 0.45f), 0.3f);
            var crust = Mat("Croissant", new Color(0.8f, 0.52f, 0.2f), 0.35f);
            var root = new GameObject("Gull");
            root.transform.SetParent(level, false);
            root.transform.SetPositionAndRotation(new Vector3(b.x + 1.6f, 0f, -97.8f), Quaternion.Euler(0f, 200f, 0f));
            Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (type == PrimitiveType.Sphere) go.GetComponent<MeshFilter>().sharedMesh = LowSphere();
                return go.transform;
            }
            Transform Pivot(string name, Transform parent, Vector3 pos)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.localPosition = pos;
                return t;
            }
            // About 60 cm long: white body and head, grey back, black wing tips, a yellow beak with a croissant.
            var body = Pivot("Body", root.transform, Vector3.zero);
            Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.2f, 0f), new Vector3(-8f, 0f, 0f), new Vector3(0.2f, 0.19f, 0.4f), white);
            Part(PrimitiveType.Cube, body, new Vector3(0f, 0.18f, -0.25f), new Vector3(-10f, 0f, 0f), new Vector3(0.09f, 0.03f, 0.16f), black);
            foreach (float side in new[] { -1f, 1f })
                Part(PrimitiveType.Cylinder, body, new Vector3(side * 0.04f, 0.05f, 0.02f), Vector3.zero, new Vector3(0.018f, 0.05f, 0.018f), pink);
            var head = Pivot("Head", body, new Vector3(0f, 0.32f, 0.16f));
            Part(PrimitiveType.Sphere, head, Vector3.zero, Vector3.zero, new Vector3(0.11f, 0.11f, 0.12f), white);
            Part(PrimitiveType.Cube, head, new Vector3(0f, -0.01f, 0.1f), new Vector3(8f, 0f, 0f), new Vector3(0.025f, 0.03f, 0.09f), yellow);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.02f, 0.17f), new Vector3(0f, 0f, 90f), new Vector3(0.035f, 0.08f, 0.035f), crust); // the croissant
            foreach (float side in new[] { -1f, 1f })
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.045f, 0.02f, 0.035f), Vector3.zero, Vector3.one * 0.016f, black);
            Transform Wing(string name, float side)
            {
                var pivot = Pivot(name, body, new Vector3(side * 0.09f, 0.26f, 0f));
                Part(PrimitiveType.Cube, pivot, new Vector3(side * 0.01f, -0.06f, -0.05f), Vector3.zero, new Vector3(0.018f, 0.13f, 0.34f), grey);
                Part(PrimitiveType.Cube, pivot, new Vector3(side * 0.012f, -0.08f, -0.24f), Vector3.zero, new Vector3(0.016f, 0.08f, 0.1f), black);
                return pivot;
            }
            var left = Wing("WingL", -1f);
            var right = Wing("WingR", 1f);
            // Perches: the bakery's front gutter and the far bank's railing posts; the yard is the bank in front.
            var perches = new[]
            {
                new Vector3(b.x + 0.1f, 7.05f, -94f), new Vector3(b.x + 0.1f, 7.05f, -100f), new Vector3(b.x + 0.1f, 7.05f, -105f),
            };
            var yard = new Vector4(b.x + 0.8f, b.z0 + 1.5f, -49.5f, b.z1 - 1.5f);
            var gull = root.AddComponent<Pigeon>();
            gull.Configure(body, head, left, right, perches, yard);
            var so = new SerializedObject(gull);
            so.FindProperty("group").stringValue = "gull";
            so.FindProperty("touchRadius").floatValue = 0.45f;
            so.FindProperty("fleeSneaking").floatValue = 0.6f;
            so.FindProperty("fleeWalking").floatValue = 1.8f;
            so.FindProperty("fleeRunning").floatValue = 3.2f;
            so.FindProperty("walkSpeed").floatValue = 0.35f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The croissant it drops, by the bakery door (hidden until the gull lets go).
            var croissant = new GameObject("Croissant");
            croissant.transform.SetParent(level, false);
            croissant.transform.position = new Vector3(b.x + 0.9f, 0.06f, -99.0f);
            Part(PrimitiveType.Sphere, croissant.transform, Vector3.zero, new Vector3(0f, 30f, 90f), new Vector3(0.06f, 0.14f, 0.06f), crust);
            croissant.AddComponent<StoryItem>().Configure("Flaky, buttery crumbs. Mmm!", 0.45f);
            croissant.SetActive(false);
        }

        /// <summary>
        /// Chapter 2: a pot of red geraniums someone left on the big gravel roof, and two butterflies (a cabbage
        /// white and a red admiral) that zigzag over it by day (Moth with dayFlier).
        /// </summary>
        static void BuildButterflies(Layout layout)
        {
            var roof = (layout.buildings ?? new Building[0]).FirstOrDefault(b => b.id == "roof_a");
            if (roof == null) return;
            var potPos = new Vector3(-13.5f, roof.h, -5f);
            var terracotta = Mat("Pot_Terracotta", new Color(0.72f, 0.38f, 0.24f), 0.15f);
            var leaf = Mat("Geranium_Leaf", new Color(0.24f, 0.45f, 0.2f), 0.2f);
            var bloom = Mat("Geranium_Red", new Color(0.86f, 0.12f, 0.14f), 0.3f);
            var pot = new GameObject("GeraniumPot").transform;
            pot.SetParent(level, false);
            pot.position = potPos;
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.transform.SetParent(pot, false);
            body.transform.localPosition = Vector3.up * 0.15f;
            body.transform.localScale = new Vector3(0.36f, 0.15f, 0.36f);
            body.GetComponent<Renderer>().sharedMaterial = terracotta;
            var rng = new System.Random(5);
            for (int i = 0; i < 7; i++)
            {
                float a = i * 0.9f, r = 0.08f + 0.06f * (float)rng.NextDouble();
                foreach (var (m, y, size) in new[] { (leaf, 0.34f, 0.13f), (bloom, 0.44f, 0.09f) })
                {
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(ball.GetComponent<Collider>());
                    ball.transform.SetParent(pot, false);
                    ball.transform.localPosition = new Vector3(Mathf.Cos(a) * r, y + 0.03f * (float)rng.NextDouble(), Mathf.Sin(a) * r);
                    ball.transform.localScale = Vector3.one * size;
                    ball.GetComponent<Renderer>().sharedMaterial = m;
                    ball.GetComponent<MeshFilter>().sharedMesh = LowSphere();
                }
            }
            var kinds = new[]
            {
                ("Butterfly_White", Mat("Butterfly_White", new Color(0.96f, 0.95f, 0.88f), 0.1f)),
                ("Butterfly_Admiral", Mat("Butterfly_Admiral", new Color(0.12f, 0.1f, 0.1f), 0.1f)),
            };
            var band = Mat("Butterfly_Band", new Color(0.9f, 0.32f, 0.08f), 0.1f);
            var dark = Mat("Bird_Black", new Color(0.04f, 0.04f, 0.045f), 0.45f);
            Vector3 centre = potPos + Vector3.up * 0.95f;
            for (int i = 0; i < kinds.Length; i++)
            {
                var (name, wingMat) = kinds[i];
                var fly = new GameObject(name);
                fly.transform.SetParent(level, false);
                fly.transform.position = centre + new Vector3(0.4f * (i == 0 ? 1f : -1f), 0f, 0f);
                var torso = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(torso.GetComponent<Collider>());
                torso.transform.SetParent(fly.transform, false);
                torso.transform.localScale = new Vector3(0.02f, 0.02f, 0.065f); // a bit bigger than life: it must read on a phone
                torso.GetComponent<Renderer>().sharedMaterial = dark;
                torso.GetComponent<MeshFilter>().sharedMesh = LowSphere();
                Transform Wing(float side)
                {
                    var pivot = new GameObject(side < 0 ? "WingL" : "WingR").transform;
                    pivot.SetParent(fly.transform, false);
                    pivot.localPosition = new Vector3(side * 0.004f, 0.004f, 0f);
                    var w = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.DestroyImmediate(w.GetComponent<Collider>());
                    w.transform.SetParent(pivot, false);
                    w.transform.localPosition = new Vector3(side * 0.05f, 0f, 0.006f);
                    w.transform.localScale = new Vector3(0.1f, 0.003f, 0.085f);
                    w.GetComponent<Renderer>().sharedMaterial = wingMat;
                    if (i == 1) // the red admiral's orange band
                    {
                        var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Object.DestroyImmediate(stripe.GetComponent<Collider>());
                        stripe.transform.SetParent(pivot, false);
                        stripe.transform.localPosition = new Vector3(side * 0.05f, 0.0025f, 0.012f);
                        stripe.transform.localScale = new Vector3(0.075f, 0.003f, 0.02f);
                        stripe.GetComponent<Renderer>().sharedMaterial = band;
                    }
                    return pivot;
                }
                var left = Wing(-1f);
                var right = Wing(1f);
                var moth = fly.AddComponent<Moth>();
                moth.Configure(centre, left, right, butterfly: true);
                var so = new SerializedObject(moth);
                so.FindProperty("radius").floatValue = 0.55f;
                so.FindProperty("speed").floatValue = 0.9f;
                so.FindProperty("diveDrop").floatValue = 0.5f;
                so.FindProperty("catchRadius").floatValue = 0.34f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Chapter 4: an old fisherman on a stool at the quay edge, his rod in a holder, a float on the river and a
        /// bucket of fish beside him. +Z of the root faces the river.
        /// </summary>
        static void BuildFisherman(Layout layout)
        {
            if (layout.water == null) return;
            var wood = Mat("Stool_Wood", new Color(0.45f, 0.33f, 0.22f), 0.2f);
            var trousers = Mat("Fisher_Trousers", new Color(0.3f, 0.28f, 0.22f), 0.2f);
            var jacket = Mat("Fisher_Jacket", new Color(0.24f, 0.36f, 0.24f), 0.2f);
            var skin = Mat("Fisher_Skin", new Color(0.86f, 0.66f, 0.52f), 0.3f);
            var cap = Mat("Fisher_Cap", new Color(0.18f, 0.2f, 0.28f), 0.2f);
            var boot = Mat("Human_Shoe", new Color(0.08f, 0.07f, 0.07f), 0.4f);
            var metal = Mat("Bucket_Metal", new Color(0.55f, 0.57f, 0.6f), 0.6f);
            var silver = Mat("Fish_Silver", new Color(0.78f, 0.8f, 0.84f), 0.85f);
            var red = Mat("Float_Red", new Color(0.9f, 0.15f, 0.1f), 0.4f);
            var rodMat = Mat("Rod", new Color(0.15f, 0.12f, 0.1f), 0.5f);
            var root = new GameObject("Fisherman").transform;
            root.SetParent(level, false);
            root.SetPositionAndRotation(new Vector3(-34.5f, 0f, -28f), Quaternion.Euler(0f, 270f, 0f));
            Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (type == PrimitiveType.Sphere) go.GetComponent<MeshFilter>().sharedMesh = LowSphere();
                return go.transform;
            }
            Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.22f, 0f), Vector3.zero, new Vector3(0.32f, 0.22f, 0.32f), wood);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cube, root, new Vector3(side * 0.11f, 0.5f, 0.2f), Vector3.zero, new Vector3(0.14f, 0.14f, 0.42f), trousers);
                Part(PrimitiveType.Cube, root, new Vector3(side * 0.11f, 0.26f, 0.4f), Vector3.zero, new Vector3(0.13f, 0.46f, 0.13f), trousers);
                Part(PrimitiveType.Cube, root, new Vector3(side * 0.11f, 0.05f, 0.46f), Vector3.zero, new Vector3(0.14f, 0.1f, 0.26f), boot);
            }
            var torso = new GameObject("Torso").transform;
            torso.SetParent(root, false);
            torso.localPosition = new Vector3(0f, 0.52f, 0f);
            Part(PrimitiveType.Cube, torso, new Vector3(0f, 0.3f, 0f), new Vector3(8f, 0f, 0f), new Vector3(0.44f, 0.58f, 0.28f), jacket);
            Part(PrimitiveType.Sphere, torso, new Vector3(0f, 0.74f, 0.03f), Vector3.zero, Vector3.one * 0.24f, skin);
            Part(PrimitiveType.Cylinder, torso, new Vector3(0f, 0.85f, 0.03f), Vector3.zero, new Vector3(0.25f, 0.04f, 0.25f), cap);
            Part(PrimitiveType.Cube, torso, new Vector3(0f, 0.83f, 0.15f), Vector3.zero, new Vector3(0.2f, 0.02f, 0.12f), cap);
            foreach (float side in new[] { -1f, 1f })
                Part(PrimitiveType.Cube, torso, new Vector3(side * 0.24f, 0.32f, 0.12f), new Vector3(20f, 0f, 0f), new Vector3(0.11f, 0.11f, 0.36f), jacket);
            // The rod rests in a holder, 30 degrees up over the river; a line down to the float.
            var holder = new GameObject("Rod").transform;
            holder.SetParent(root, false);
            holder.localPosition = new Vector3(0.3f, 0.35f, 0.3f);
            holder.localRotation = Quaternion.Euler(60f, 0f, 0f);
            Part(PrimitiveType.Cylinder, holder, new Vector3(0f, 1.5f, 0f), Vector3.zero, new Vector3(0.025f, 1.5f, 0.025f), rodMat);
            Vector3 tip = holder.TransformPoint(new Vector3(0f, 3f, 0f));
            Vector3 floatPos = new Vector3(tip.x - 1.2f, layout.water.top + 0.02f, tip.z);
            var line = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(line.GetComponent<Collider>());
            line.transform.SetParent(root, true);
            line.transform.position = (tip + floatPos) / 2;
            line.transform.rotation = Quaternion.FromToRotation(Vector3.up, tip - floatPos);
            line.transform.localScale = new Vector3(0.004f, Vector3.Distance(tip, floatPos) / 2, 0.004f);
            line.GetComponent<Renderer>().sharedMaterial = Mat("Fishing_Line", new Color(0.85f, 0.85f, 0.8f), 0.3f);
            var bobber = Part(PrimitiveType.Sphere, root, Vector3.zero, Vector3.zero, Vector3.one * 0.07f, red);
            bobber.position = floatPos;
            // The bucket, beside him on the quay side, three fish on top.
            var bucket = new GameObject("FishBucket").transform;
            bucket.SetParent(level, false);
            bucket.position = new Vector3(-33.8f, 0f, -27.3f);
            Part(PrimitiveType.Cylinder, bucket, new Vector3(0f, 0.16f, 0f), Vector3.zero, new Vector3(0.3f, 0.16f, 0.3f), metal);
            for (int i = 0; i < 3; i++)
                Part(PrimitiveType.Sphere, bucket, new Vector3(-0.06f + i * 0.06f, 0.33f, 0.02f * i), new Vector3(0f, 30f * i, 0f), new Vector3(0.05f, 0.04f, 0.16f), silver);
            root.gameObject.AddComponent<Fisherman>().Configure(torso, bucket, bobber);
        }

        /// <summary>The additive, two-sided glow material of the sunbeam, the glints and the lamp cones.</summary>
        static Material AdditiveMaterial()
        {
            string path = $"{PlaygroundBuilder.MaterialDir}/SunBeam.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f); // additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Rain splashes on the ground round the camera in the storm (RainSplashes): tiny fast drops.</summary>
        static void BuildRainSplashes()
        {
            var puff = AssetDatabase.LoadAssetAtPath<Material>($"{PlaygroundBuilder.MaterialDir}/Dust.mat");
            if (puff == null) return;
            var go = new GameObject("RainSplashes");
            go.transform.SetParent(level, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.055f);
            main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.7f);
            main.gravityModifier = 1.5f;
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.enabled = false; // only Emit() at the hit points
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere; // a little crown, up and out
            shape.radius = 0.02f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = puff;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<RainSplashes>();
        }

        /// <summary>Eight places to scratch (ScratchSpot): upright, rough things that are not climbable.</summary>
        static void BuildScratchSpots()
        {
            var spots = new (string id, string title, Vector3 pos)[]
            {
                ("scratch_sisal", "your own scratching post (the only one you are allowed)", new Vector3(4.1f, 0.5f, 4.0f)),
                ("scratch_sofa", "the sofa. The humans will not be pleased", new Vector3(2.0f, 0.5f, 3.9f)),
                ("scratch_table", "a leg of the garden table", new Vector3(-2.35f, 0.5f, 1.8f)),
                ("scratch_fence", "the old garden fence", new Vector3(-10f, 0.5f, 4.7f)),
                ("scratch_chair", "the old chair in the wild garden", new Vector3(-17.5f, 0.5f, 7.1f)),
                ("scratch_lamp", "the iron lamp-post on the quay", new Vector3(-33.6f, 0.5f, 3.2f)),
                ("scratch_bakery", "the bakery's wooden door frame", new Vector3(-57.6f, 0.5f, -100.9f)),
                ("scratch_church", "the church door of the begijnhof", new Vector3(51.6f, 0.8f, -208.6f)),
            };
            var ringMat = NoiseRingMaterial();
            var marks = Mat("Claw_Marks", new Color(0.12f, 0.09f, 0.07f), 0.05f);
            foreach (var (id, title, pos) in spots)
            {
                var go = new GameObject($"Scratch_{id.Substring(8)}");
                go.transform.SetParent(level, false);
                go.transform.position = pos;
                go.AddComponent<LineRenderer>().sharedMaterial = ringMat;
                go.AddComponent<ScratchSpot>().Configure(id, title, marks);
            }
        }

        /// <summary>Morning mist over the gardens: big faint billboard puffs (MorningMist), prewarmed.</summary>
        static void BuildMorningMist(Layout layout)
        {
            var puff = AssetDatabase.LoadAssetAtPath<Material>($"{PlaygroundBuilder.MaterialDir}/Dust.mat"); // made with the cat
            if (puff == null) { Debug.LogWarning("Dust.mat not found: no morning mist"); return; }
            var w = layout.wild_garden;
            float x0 = w != null ? w.x0 : -26f, x1 = 0f, z0 = 0.3f, z1 = w != null ? w.z1 : 9.5f;
            var go = new GameObject("MorningMist");
            go.transform.SetParent(level, false);
            go.transform.position = new Vector3((x0 + x1) / 2f, 0.6f, (z0 + z1) / 2f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(16f, 24f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new Color(0.92f, 0.94f, 0.97f, 1f);
            main.maxParticles = 160;
            var emission = ps.emission;
            emission.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(x1 - x0, 0.8f, z1 - z0);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.05f, 0.12f); // a faint drift down the gardens
            vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = puff; // soft round puffs, alpha blend
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.maxParticleSize = 3f;
            go.AddComponent<MorningMist>();
        }

        /// <summary>The visible bolt of each lightning flash in the storm (LightningBolt, the Draho/Bolt shader).</summary>
        static void BuildLightningBolt()
        {
            var shader = Shader.Find("Draho/Bolt");
            if (shader == null) { Debug.LogWarning("Draho/Bolt not found: no lightning bolts"); return; }
            string path = $"{PlaygroundBuilder.MaterialDir}/Bolt.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            var go = new GameObject("LightningBolt");
            go.transform.SetParent(level, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<LightningBolt>();
        }

        /// <summary>Shooting stars on clear nights (ShootingStars, the Draho/Meteor shader).</summary>
        static void BuildShootingStars()
        {
            var shader = Shader.Find("Draho/Meteor");
            if (shader == null) { Debug.LogWarning("Draho/Meteor not found: no shooting stars"); return; }
            string path = $"{PlaygroundBuilder.MaterialDir}/Meteor.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            m.SetFloat("_Width", 3f);
            EditorUtility.SetDirty(m);
            var go = new GameObject("ShootingStars");
            go.transform.SetParent(level, false);
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<ShootingStars>();
        }

        /// <summary>Five viewpoints on the high roofs: a faint blue ring; stand still in it for a slow camera sweep.</summary>
        static void BuildViewpoints()
        {
            var views = new (string id, string text, Vector3 pos)[]
            {
                ("view_ridge", "the ridge of the big roof. The whole street lies below, and the chimneys smoke.", new Vector3(14.4f, 12.4f, 4.6f)),
                ("view_highroof", "the highest roof of the block. Gardens, roofs and the river, all yours.", new Vector3(-5.2f, 9.6f, -7.9f)),
                ("view_flatroof", "the flat roof by the hatch. Every garden of the block in one look.", new Vector3(3.2f, 9.4f, 5.6f)),
                ("view_gravel", "the big gravel roof. The geraniums, the light well and the maple.", new Vector3(-15.5f, 7.1f, -5.6f)),
                ("view_riverhouse", "the riverhouse roof. The river, the quay and the far bank.", new Vector3(-25.6f, 7.0f, -12f)),
            };
            var mat = NoiseRingMaterial();
            foreach (var (id, text, pos) in views)
            {
                var go = new GameObject($"Viewpoint_{id.Substring(5)}");
                go.transform.SetParent(level, false);
                go.transform.position = pos;
                go.AddComponent<LineRenderer>().sharedMaterial = mat;
                go.AddComponent<Viewpoint>().Configure(id, text);
            }
        }

        /// <summary>Six gifts for the humans, one in each area, shown only in the epilogue (Gift).</summary>
        static void BuildGifts()
        {
            var red = Mat("Float_Red", new Color(0.9f, 0.15f, 0.1f), 0.4f);
            var white = Mat("Feather_White", new Color(0.95f, 0.94f, 0.9f), 0.3f);
            var crust = Mat("Croissant", new Color(0.8f, 0.52f, 0.2f), 0.35f);
            var gold = Mat("Apt_Gold", new Color(0.85f, 0.65f, 0.2f), 0.7f);
            var sock = Mat("Gift_Sock", new Color(0.75f, 0.2f, 0.35f), 0.1f);
            var ribbon = Mat("Gift_Ribbon", new Color(0.2f, 0.35f, 0.8f), 0.5f);
            void Piece(Transform parent, PrimitiveType type, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localEulerAngles = euler;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            var gifts = new (string id, string title, string reaction, Vector3 pos)[]
            {
                ("gift_sock", "an odd sock from the wild garden", "\"Wait, that's not even our sock!\"", new Vector3(-16.4f, 1.2f, 7.4f)),
                ("gift_float", "a red fishing float from the quay", "\"The fisherman will be looking for this...\"", new Vector3(-31.8f, 0.6f, -23.6f)),
                ("gift_crust", "a croissant crust from the bakery", "\"Breakfast in bed? How kind.\"", new Vector3(-56.4f, 0.6f, -101.4f)),
                ("gift_cap", "a shiny bottle cap from the court", "\"Ooh, shiny. Thank you, Drahomil.\"", new Vector3(5.6f, 0.6f, 4.0f)),
                ("gift_ribbon", "a blue ribbon from the begijnhof", "\"From the begijnhof? You went all that way?\"", new Vector3(50.4f, 0.9f, -208.2f)),
                ("gift_gullfeather", "a gull feather from the highest roof", "\"You were on the ROOF?\"", new Vector3(-5.0f, 9.2f, -7.2f)),
            };
            foreach (var (id, title, reaction, pos) in gifts)
            {
                var root = new GameObject($"Gift_{id.Substring(5)}").transform;
                root.SetParent(level, false);
                root.position = pos;
                switch (id)
                {
                    case "gift_sock":
                        Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), Vector3.zero, new Vector3(0.05f, 0.025f, 0.14f), sock);
                        Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.015f, 0.08f), new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.025f, 0.05f), white);
                        break;
                    case "gift_float":
                        Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.03f, 0f), Vector3.zero, Vector3.one * 0.05f, red);
                        Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.008f, 0.05f, 0.008f), white);
                        break;
                    case "gift_crust":
                        Piece(root, PrimitiveType.Capsule, new Vector3(0f, 0.02f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.035f, 0.05f, 0.035f), crust);
                        break;
                    case "gift_cap":
                        Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.006f, 0f), Vector3.zero, new Vector3(0.035f, 0.006f, 0.035f), gold);
                        break;
                    case "gift_ribbon":
                        Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.005f, 0f), new Vector3(0f, 20f, 0f), new Vector3(0.025f, 0.006f, 0.16f), ribbon);
                        Piece(root, PrimitiveType.Cube, new Vector3(0.02f, 0.008f, 0.03f), new Vector3(0f, -35f, 0f), new Vector3(0.025f, 0.006f, 0.12f), ribbon);
                        break;
                    default:
                        Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.005f, 0f), Vector3.zero, new Vector3(0.035f, 0.004f, 0.2f), white);
                        break;
                }
                root.gameObject.AddComponent<Gift>().Configure(id, title, reaction);
            }
        }

        /// <summary>
        /// Cloud shadows: the cookie is saved as an asset and put on the sun in the scene, so the build keeps the light
        /// cookie shader variant; CloudShadows slides it with the wind by day.
        /// </summary>
        static void BuildCloudShadows()
        {
            new GameObject("CloudShadows").AddComponent<CloudShadows>().transform.SetParent(level, false);
            var sunGo = GameObject.Find("Sun");
            if (sunGo == null) return;
            string path = $"{PlaygroundBuilder.MaterialDir}/CloudCookie.asset";
            AssetDatabase.DeleteAsset(path); // remade every build, so a new cookie reaches the game
            var tex = CloudShadows.MakeCookie(128, 0.5f);
            AssetDatabase.CreateAsset(tex, path);
            var sun = sunGo.GetComponent<Light>();
            sun.cookie = tex;
            var data = sunGo.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            if (data == null) data = sunGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();
            data.lightCookieSize = new Vector2(60f, 60f);
        }

        /// <summary>Glow-worms in the wild garden's long grass at night (GlowWorms; the additive glow material).</summary>
        static void BuildGlowWorms(WildGarden w)
        {
            if (w == null) return;
            var go = new GameObject("GlowWorms");
            go.transform.SetParent(level, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AdditiveMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<GlowWorms>().Configure(new Vector3(w.x0 + 0.3f, 0f, w.z0 + 0.25f), new Vector3(w.x1 - 0.5f, 0f, w.z1 - 0.2f));
        }

        /// <summary>A soft cone of lamplight from a lamp down to the ground (LampCone).</summary>
        static void AddLampCone(Transform parent, Vector3 apex, float height, float radius, Light light)
        {
            var go = new GameObject("LampCone");
            go.transform.SetParent(parent, false);
            go.transform.position = apex;
            go.AddComponent<MeshFilter>();
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = AdditiveMaterial();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.AddComponent<LampCone>().Configure(light, height, radius);
        }

        /// <summary>
        /// A shaft of low sun through the sliding glass door, with dust motes (SunBeam builds the shaft at runtime from
        /// the sun's direction). Additive, both faces, no shadows.
        /// </summary>
        static void BuildSunBeam(Layout layout)
        {
            var door = (layout.openings ?? new Opening[0]).FirstOrDefault(o => o.id == "sliding_door");
            if (door == null) return;
            var mat = AdditiveMaterial();

            var beam = new GameObject("SunBeam");
            beam.transform.SetParent(level, false);
            beam.AddComponent<MeshFilter>();
            var r = beam.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            float x = 0.03f, z0 = door.from + 0.05f, z1 = door.to - 0.05f, top = door.height - 0.05f;
            var opening = new[] { new Vector3(x, 0.05f, z0), new Vector3(x, top, z0), new Vector3(x, top, z1), new Vector3(x, 0.05f, z1) };

            // Dust motes: a few slow specks in the living room, emitted only while the sun shines in.
            var motesGo = new GameObject("Motes");
            motesGo.transform.SetParent(beam.transform, false);
            motesGo.transform.position = new Vector3(1.6f, 1.2f, (z0 + z1) / 2);
            var ps = motesGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.025f);
            main.startColor = new Color(1f, 0.92f, 0.75f, 0.7f);
            main.maxParticles = 60;
            main.gravityModifier = -0.002f;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.8f, 1.8f, z1 - z0);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var pr = motesGo.GetComponent<ParticleSystemRenderer>();
            pr.shadowCastingMode = ShadowCastingMode.Off;
            var dust = AssetDatabase.LoadAssetAtPath<Material>($"{PlaygroundBuilder.MaterialDir}/Dust.mat");
            if (dust != null) pr.sharedMaterial = dust;
            beam.AddComponent<SunBeam>().Configure(opening, 4.4f, ps);
        }

        /// <summary>
        /// Chapter 5: a raised bed of catnip in the begijnhof herb garden behind the church, and a scent trail of faint
        /// green wisps from the grey cat's step to it (shown by the story).
        /// </summary>
        static void BuildCatnip(Layout layout)
        {
            if (layout.begijnhof == null) return;
            var bedPos = new Vector3(60.4f, 0f, -217.2f);
            var wood = Mat("Bed_Wood", new Color(0.42f, 0.3f, 0.2f), 0.15f);
            var leaf = Mat("Catnip_Leaf", new Color(0.45f, 0.62f, 0.38f), 0.2f);
            var flower = Mat("Catnip_Flower", new Color(0.78f, 0.7f, 0.9f), 0.3f);
            var bed = new GameObject("CatnipBed");
            bed.transform.SetParent(level, false);
            bed.transform.position = bedPos;
            Box("BedFrame", bedPos + Vector3.up * 0.12f, new Vector3(1.2f, 0.24f, 0.7f), wood).transform.SetParent(bed.transform, true);
            var rng = new System.Random(52);
            for (int i = 0; i < 14; i++)
            {
                var p = new Vector3(-0.5f + (float)rng.NextDouble(), 0.3f, -0.27f + 0.54f * (float)rng.NextDouble());
                foreach (var (m, dy, size) in new[] { (leaf, 0f, 0.16f), (flower, 0.1f, 0.05f) })
                {
                    var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(ball.GetComponent<Collider>());
                    ball.transform.SetParent(bed.transform, false);
                    ball.transform.localPosition = p + Vector3.up * dy;
                    ball.transform.localScale = Vector3.one * size * (0.8f + 0.4f * (float)rng.NextDouble());
                    ball.GetComponent<Renderer>().sharedMaterial = m;
                    ball.GetComponent<MeshFilter>().sharedMesh = LowSphere();
                }
            }
            // The scent trail: a few soft green wisps rising along a curve from the church step to the bed.
            var dust = AssetDatabase.LoadAssetAtPath<Material>($"{PlaygroundBuilder.MaterialDir}/Dust.mat");
            var points = new[] { new Vector3(54.6f, 0f, -210.8f), new Vector3(56.6f, 0f, -212.2f), new Vector3(58.2f, 0f, -213.9f),
                                 new Vector3(59.4f, 0f, -215.5f), new Vector3(60.1f, 0f, -216.6f) };
            var wisps = new List<ParticleSystem>();
            foreach (var point in points)
            {
                var go = new GameObject("ScentWisp");
                go.transform.SetParent(bed.transform, true);
                go.transform.position = point + Vector3.up * 0.15f;
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = true;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.42f);
                main.startColor = new Color(0.55f, 1f, 0.45f, 0.6f);
                main.maxParticles = 12;
                var emission = ps.emission;
                emission.rateOverTime = 4f;
                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.25f;
                var fade = ps.colorOverLifetime;
                fade.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
                fade.color = g;
                var pr = go.GetComponent<ParticleSystemRenderer>();
                pr.shadowCastingMode = ShadowCastingMode.Off;
                if (dust != null) pr.sharedMaterial = dust;
                wisps.Add(ps);
            }
            bed.AddComponent<Catnip>().Configure(wisps.ToArray());
        }

        /// <summary>
        /// Twelve nap spots, the warmest places in every area (a side activity): sit in one to find it. Each spot drops
        /// onto whatever is under it at run time (a rug, a table top, a roof).
        /// </summary>
        static void BuildNapSpots()
        {
            var spots = new (string id, string title, Vector3 pos)[]
            {
                ("nap_rug", "the red rug in the living room", new Vector3(2.2f, 0.5f, 2.6f)),
                ("nap_cattree", "your own basket on the cat tree", new Vector3(4.1f, 1.2f, 4.35f)),
                ("nap_table", "the garden table, still warm from the sun", new Vector3(-1.4f, 1.2f, 2.6f)),
                ("nap_maple", "the shade under the maple", new Vector3(-21.2f, 0.5f, 3.3f)),
                ("nap_gravel", "the warm gravel by the geraniums", new Vector3(-14.6f, 7f, -3.8f)),
                ("nap_high", "the highest roof of the block", new Vector3(-5.6f, 9.1f, -7.5f)),
                ("nap_chair", "the old chair in the wild garden", new Vector3(-17f, 1.2f, 7.6f)),
                ("nap_quay", "the sunny quay stones", new Vector3(-32.4f, 0.5f, -24f)),
                ("nap_church", "the church step of the begijnhof", new Vector3(51.2f, 0.8f, -208.8f)),
                ("nap_bakery", "the bakery doorstep, smelling of bread", new Vector3(-57.1f, 0.5f, -100.6f)),
                ("nap_court", "the sunny corner of the court", new Vector3(5.1f, 0.5f, 4.6f)),
                ("nap_roof", "the flat roof of your own house", new Vector3(2.4f, 9.3f, 2.6f)),
            };
            var mat = NoiseRingMaterial();
            foreach (var (id, title, pos) in spots)
            {
                var go = new GameObject($"NapSpot_{id.Substring(4)}");
                go.transform.SetParent(level, false);
                go.transform.position = pos;
                go.AddComponent<LineRenderer>().sharedMaterial = mat;
                go.AddComponent<NapSpot>().Configure(id, title);
            }
            var rested = new GameObject("WellRested");
            rested.transform.SetParent(level, false);
            rested.AddComponent<WellRested>().Configure(spots.Length);
        }

        static void BuildFox()
        {
            var orange = Mat("Fox_Orange", new Color(0.78f, 0.36f, 0.12f), 0.2f);
            var white = Mat("Fox_White", new Color(0.78f, 0.75f, 0.7f), 0.05f); // creamy, not bright at night
            var black = Mat("Fox_Black", new Color(0.08f, 0.07f, 0.07f), 0.3f);
            var root = new GameObject("Fox");
            root.transform.SetParent(level, false);
            root.transform.position = new Vector3(-3.5f, 0f, 8.6f);
            Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                if (type == PrimitiveType.Sphere) go.GetComponent<MeshFilter>().sharedMesh = LowSphere();
                return go.transform;
            }
            Transform Pivot(string name, Transform parent, Vector3 pos)
            {
                var t = new GameObject(name).transform;
                t.SetParent(parent, false);
                t.localPosition = pos;
                return t;
            }
            var r = root.transform;
            Part(PrimitiveType.Sphere, r, new Vector3(0f, 0.33f, 0f), Vector3.zero, new Vector3(0.2f, 0.2f, 0.48f), orange);
            Part(PrimitiveType.Sphere, r, new Vector3(0f, 0.27f, 0.2f), Vector3.zero, new Vector3(0.11f, 0.12f, 0.12f), white); // chest
            var head = Pivot("Head", r, new Vector3(0f, 0.42f, 0.26f));
            Part(PrimitiveType.Sphere, head, Vector3.zero, Vector3.zero, new Vector3(0.15f, 0.13f, 0.15f), orange);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.025f, 0.1f), Vector3.zero, new Vector3(0.07f, 0.06f, 0.13f), orange); // snout
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.045f, 0.09f), Vector3.zero, new Vector3(0.06f, 0.035f, 0.1f), white); // white jaw
            Part(PrimitiveType.Sphere, head, new Vector3(0f, -0.015f, 0.165f), Vector3.zero, Vector3.one * 0.025f, black);          // nose
            foreach (float side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Cube, head, new Vector3(side * 0.045f, 0.08f, -0.01f), new Vector3(0f, 0f, side * -12f), new Vector3(0.05f, 0.09f, 0.02f), black); // ears
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.04f, 0.02f, 0.06f), Vector3.zero, Vector3.one * 0.018f, black);   // eyes
            }
            var legs = new List<Transform>();
            foreach (float z in new[] { 0.16f, -0.16f })
                foreach (float side in new[] { -1f, 1f })
                {
                    var leg = Pivot("Leg", r, new Vector3(side * 0.06f, 0.27f, z));
                    Part(PrimitiveType.Cube, leg, new Vector3(0f, -0.135f, 0f), Vector3.zero, new Vector3(0.04f, 0.27f, 0.045f), black);
                    legs.Add(leg);
                }
            var tail = Pivot("Tail", r, new Vector3(0f, 0.36f, -0.22f));
            Part(PrimitiveType.Sphere, tail, new Vector3(0f, -0.05f, -0.17f), new Vector3(-15f, 0f, 0f), new Vector3(0.12f, 0.12f, 0.38f), orange);
            Part(PrimitiveType.Sphere, tail, new Vector3(0f, -0.1f, -0.35f), new Vector3(-15f, 0f, 0f), new Vector3(0.085f, 0.085f, 0.11f), white);
            // The route: from the workshop end of the wild garden to the far corner, sniffing on the way.
            var route = new[]
            {
                new Vector3(-3.5f, 0f, 8.6f), new Vector3(-7f, 0f, 7.2f), new Vector3(-11f, 0f, 8.3f), new Vector3(-15f, 0f, 6.5f),
                new Vector3(-19f, 0f, 7.8f), new Vector3(-22.5f, 0f, 6.9f), new Vector3(-24.6f, 0f, 6.3f),
            };
            var sniff = new[] { false, true, true, true, false, true, false };
            root.AddComponent<Fox>().Configure(head, tail, legs.ToArray(), route, sniff);
        }

        static void BuildCourtLife(Layout layout)
        {
            var c = (layout.courts ?? new Court[0]).FirstOrDefault(x => x.shaft_height > 0f);
            if (c == null) return;
            float midX = (c.x0 + c.x1) / 2, midZ = (c.z0 + c.z1) / 2, top = c.shaft_height;
            var perches = new List<Vector3>();
            foreach (float floorY in new[] { 3.3f, 6.1f })
            {
                perches.Add(new Vector3(midX - 0.45f, floorY, c.z1 - 0.1f));
                perches.Add(new Vector3(midX + 0.45f, floorY, c.z1 - 0.1f));
                perches.Add(new Vector3(c.x0 + 0.1f, floorY, midZ));
            }
            perches.Add(new Vector3(c.x0 + 0.5f, top, c.z1 - 0.08f));
            perches.Add(new Vector3(midX, top, c.z1 - 0.08f));
            perches.Add(new Vector3(c.x1 - 0.08f, top, midZ - 0.4f));
            var yard = new Vector4(c.x0 + 0.3f, c.z0 + 0.3f, c.x1 - 0.3f, c.z1 - 0.3f);

            var grey = Mat("Pigeon_Grey", new Color(0.56f, 0.58f, 0.62f), 0.25f);
            var darkGrey = Mat("Pigeon_Head", new Color(0.4f, 0.42f, 0.47f), 0.25f);
            var neck = Mat("Pigeon_Neck", new Color(0.3f, 0.48f, 0.42f), 0.7f); // the green-purple sheen
            var bar = Mat("Pigeon_Bar", new Color(0.18f, 0.18f, 0.2f), 0.3f);
            var pink = Mat("Pigeon_Feet", new Color(0.85f, 0.42f, 0.45f), 0.3f);
            var eye = Mat("Bird_Beak", new Color(0.95f, 0.6f, 0.08f), 0.5f);
            var spots = new[] { new Vector3(c.x0 + 0.8f, 0f, c.z0 + 0.6f), new Vector3(midX + 0.3f, 0f, c.z1 - 0.5f), new Vector3(c.x1 - 0.5f, 0f, c.z0 + 0.5f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var root = new GameObject($"Pigeon_{i + 1}");
                root.transform.SetParent(level, false);
                root.transform.SetPositionAndRotation(spots[i], Quaternion.Euler(0f, 70f + i * 110f, 0f));
                Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
                {
                    var go = GameObject.CreatePrimitive(type);
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = pos;
                    go.transform.localRotation = Quaternion.Euler(euler);
                    go.transform.localScale = scale;
                    go.GetComponent<Renderer>().sharedMaterial = m;
                    if (type == PrimitiveType.Sphere) go.GetComponent<MeshFilter>().sharedMesh = LowSphere(); // small: few triangles
                    return go.transform;
                }
                Transform Pivot(string name, Transform parent, Vector3 pos)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(parent, false);
                    t.localPosition = pos;
                    return t;
                }
                // About 32 cm from beak to tail: a plump grey body, a small dark head, a shiny neck. +Z is forward.
                var body = Pivot("Body", root.transform, Vector3.zero);
                Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.1f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(0.12f, 0.11f, 0.2f), grey);
                Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.13f, 0.065f), Vector3.zero, new Vector3(0.085f, 0.09f, 0.08f), neck);
                Part(PrimitiveType.Cube, body, new Vector3(0f, 0.11f, -0.15f), new Vector3(-12f, 0f, 0f), new Vector3(0.07f, 0.015f, 0.12f), darkGrey);
                foreach (float side in new[] { -1f, 1f })
                    Part(PrimitiveType.Cylinder, body, new Vector3(side * 0.025f, 0.025f, 0.01f), Vector3.zero, new Vector3(0.012f, 0.025f, 0.012f), pink);
                var head = Pivot("Head", body, new Vector3(0f, 0.15f, 0.06f));
                Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.025f, 0.025f), Vector3.zero, Vector3.one * 0.06f, darkGrey);
                Part(PrimitiveType.Cube, head, new Vector3(0f, 0.018f, 0.062f), new Vector3(10f, 0f, 0f), new Vector3(0.012f, 0.012f, 0.03f), bar);
                foreach (float side in new[] { -1f, 1f })
                    Part(PrimitiveType.Sphere, head, new Vector3(side * 0.025f, 0.035f, 0.035f), Vector3.zero, Vector3.one * 0.012f, eye);
                Transform Wing(string name, float side)
                {
                    var pivot = Pivot(name, body, new Vector3(side * 0.055f, 0.14f, 0f));
                    Part(PrimitiveType.Cube, pivot, new Vector3(side * 0.008f, -0.04f, -0.02f), Vector3.zero, new Vector3(0.012f, 0.085f, 0.17f), grey);
                    Part(PrimitiveType.Cube, pivot, new Vector3(side * 0.015f, -0.05f, -0.05f), Vector3.zero, new Vector3(0.004f, 0.06f, 0.012f), bar); // wing bars
                    Part(PrimitiveType.Cube, pivot, new Vector3(side * 0.015f, -0.05f, -0.08f), Vector3.zero, new Vector3(0.004f, 0.06f, 0.012f), bar);
                    return pivot;
                }
                var left = Wing("WingL", -1f);
                var right = Wing("WingR", 1f);
                root.AddComponent<Pigeon>().Configure(body, head, left, right, perches.ToArray(), yard);
            }

            // The jingle ball: behind a bracket of the court drainpipe, just under the top of the climb.
            var pipe = (layout.climbables ?? new ClimbableInfo[0]).FirstOrDefault(x => x.id == "court_pipe");
            if (pipe == null) return;
            var ball = new GameObject("JingleBall");
            ball.transform.SetParent(level, false);
            ball.transform.position = new Vector3(pipe.x - 0.1f, pipe.climb_top - 0.25f, pipe.z + 0.13f);
            var shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(shell.GetComponent<Collider>());
            shell.transform.SetParent(ball.transform, false);
            shell.transform.localScale = Vector3.one * 0.06f;
            shell.GetComponent<Renderer>().sharedMaterial = Mat("Toy_Bell", new Color(0.95f, 0.75f, 0.25f), 0.85f);
            var slot = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(slot.GetComponent<Collider>());
            slot.transform.SetParent(ball.transform, false);
            slot.transform.localPosition = new Vector3(0f, 0f, 0f);
            slot.transform.localScale = new Vector3(0.062f, 0.006f, 0.03f);
            slot.GetComponent<Renderer>().sharedMaterial = Mat("Bird_Black", new Color(0.04f, 0.04f, 0.045f), 0.45f);
            Box("BallBracket", new Vector3(pipe.x - 0.05f, ball.transform.position.y - 0.04f, pipe.z + 0.08f), new Vector3(0.14f, 0.02f, 0.2f),
                Mat($"Apt_{pipe.id}", Hex(pipe.color), 0.5f), collider: false);
            ball.AddComponent<StoryItem>().Configure("Ting-a-ling! Your old jingle ball!", 0.5f);
        }

        // ------------------------------------------------------------------ furniture
        static void BuildProp(Prop p)
        {
            var root = new GameObject(p.id).transform;
            root.SetParent(level, false);
            root.position = new Vector3(p.x, p.y, p.z);
            float yaw = FacingYaw(p.facing);
            Color color = Hex(p.color);

            if (p.id == "cat_tree") { CatTree(root, p, color); return; }
            if (p.id == "picnic_table") { PicnicTable(root, p, color); return; }
            if (p.id == "firewood") { Firewood(root, p, color); return; }

            var parts = string.IsNullOrEmpty(p.kenney)
                ? new List<string>()
                : p.kenney.Split('+').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            var onTop = parts.Where(s => s.Contains("CoffeeMachine") || s.StartsWith("plant")).ToList();
            var body = parts.Except(onTop).ToList();
            bool hasTop = !string.IsNullOrEmpty(p.top_color);
            float bodyH = hasTop ? p.h - 0.03f : p.h;

            if (body.Count == 0)
                Box("Shape", root.position + Vector3.up * p.h / 2, new Vector3(p.w, p.h, p.d), Mat($"Apt_{p.id}", color, 0.3f),
                    collider: false).transform.SetParent(root, true);
            else if (body.Count == 1)
                Fit(root, body[0], Vector3.zero, new Vector3(p.w, bodyH, p.d), yaw, color);
            else
            {
                // A row of cabinet modules along the side that does not face the room.
                bool alongX = p.facing == "south" || p.facing == "north";
                float length = alongX ? p.w : p.d;
                int n = Mathf.Max(body.Count, Mathf.RoundToInt(length / ModuleWidth));
                var modules = Enumerable.Repeat(body[0], n).ToArray();
                for (int i = 1; i < body.Count; i++) modules[i * n / body.Count] = body[i];
                float step = length / n;
                for (int i = 0; i < n; i++)
                {
                    float offset = -length / 2 + step * (i + 0.5f);
                    var local = alongX ? new Vector3(offset, 0, 0) : new Vector3(0, 0, offset);
                    var size = alongX ? new Vector3(step, bodyH, p.d) : new Vector3(p.w, bodyH, step);
                    Fit(root, modules[i], local, size, yaw, color);
                }
            }

            if (hasTop)
            {
                var top = Mat($"Apt_{p.id}_top", Hex(p.top_color), 0.5f);
                Box("Worktop", root.position + Vector3.up * (p.h - 0.015f), new Vector3(p.w + 0.03f, 0.03f, p.d + 0.03f), top,
                    collider: false).transform.SetParent(root, true);
                if (p.kenney != null && p.kenney.Contains("Stove"))
                {
                    var hob = Mat("Apt_Hob", new Color(0.03f, 0.03f, 0.035f), 0.9f);
                    Box("Hob", root.position + Vector3.up * (p.h + 0.002f), new Vector3(0.58f, 0.004f, 0.5f), hob, collider: false)
                        .transform.SetParent(root, true);
                }
            }
            foreach (var item in onTop)
            {
                bool alongX = p.facing == "south" || p.facing == "north";
                float length = alongX ? p.w : p.d;
                var local = (alongX ? Vector3.right : Vector3.forward) * (-length / 2 + 0.3f) + Vector3.up * p.h;
                Fit(root, item, local, new Vector3(0.28f, 0.36f, 0.28f), yaw, Color.white);
            }

            if (p.h > 0.05f) // rugs are walk-over
            {
                var col = root.gameObject.AddComponent<BoxCollider>();
                col.center = new Vector3(0, p.h / 2, 0);
                col.size = new Vector3(p.w, p.h, p.d);
            }
        }

        /// <summary>Instantiates a Kenney model and scales it to fill exactly the given box (bottom at local.y).</summary>
        static void Fit(Transform parent, string model, Vector3 local, Vector3 size, float yaw, Color color)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{KenneyDir}/{model}.fbx");
            if (asset == null)
            {
                Debug.LogWarning($"Draho: Kenney model '{model}' not found; using a box.");
                Box(model, parent.position + local + Vector3.up * size.y / 2, size, Mat($"Apt_box_{model}", color, 0.3f),
                    collider: false).transform.SetParent(parent, true);
                return;
            }
            var slot = new GameObject(model).transform;
            slot.SetParent(parent, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, slot);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one;
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            Bounds b = WorldBounds(go);
            slot.localScale = new Vector3(size.x / Mathf.Max(b.size.x, 1e-4f), size.y / Mathf.Max(b.size.y, 1e-4f),
                size.z / Mathf.Max(b.size.z, 1e-4f));
            b = WorldBounds(go);
            Vector3 want = parent.position + local;
            slot.position += new Vector3(want.x - b.center.x, want.y - b.min.y, want.z - b.center.z);

            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = r.sharedMaterials.Select(m => Recolor(m, color)).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On;
            }
        }

        /// <summary>Kenney materials are named by role (wood, carpet, metal, glass, plant...): map them to our colours.</summary>
        static Material Recolor(Material source, Color main)
        {
            string n = source != null ? source.name.ToLowerInvariant() : "";
            if (n.Contains("glass")) return GlassMat();
            if (n.Contains("plant")) return Mat("Apt_Plant", new Color(0.24f, 0.48f, 0.22f), 0.3f);
            if (n.Contains("metaldark")) return Mat("Apt_MetalDark", new Color(0.12f, 0.12f, 0.13f), 0.6f);
            if (n.Contains("metalmedium") || n == "metal") return Mat("Apt_Metal", new Color(0.55f, 0.56f, 0.58f), 0.6f);
            if (n.Contains("dark")) return Mat($"Apt_{ColorUtility.ToHtmlStringRGB(main)}_dark", new Color(main.r * 0.7f, main.g * 0.7f, main.b * 0.7f), 0.3f);
            return Mat($"Apt_{ColorUtility.ToHtmlStringRGB(main)}", main, 0.3f);
        }

        static void CatTree(Transform root, Prop p, Color color)
        {
            var carpet = Mat("Apt_CatTreeCarpet", color, 0.05f);
            var sisal = Mat("Apt_Sisal", new Color(0.78f, 0.7f, 0.55f), 0.05f);
            Vector3 o = root.position;
            Box("Base", o + Vector3.up * 0.15f, new Vector3(p.w, 0.3f, p.d), carpet, collider: false).transform.SetParent(root, true);
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            Object.DestroyImmediate(post.GetComponent<Collider>());
            post.transform.SetParent(root, false);
            post.transform.localPosition = new Vector3(0f, 0.3f + (p.h - 0.34f) / 2, 0f);
            post.transform.localScale = new Vector3(0.1f, (p.h - 0.34f) / 2, 0.1f);
            post.GetComponent<Renderer>().sharedMaterial = sisal;
            Box("Platform", o + Vector3.up * (p.h - 0.02f), new Vector3(p.w, 0.04f, p.d), carpet, collider: false)
                .transform.SetParent(root, true);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, p.h / 2, 0);
            col.size = new Vector3(p.w, p.h, p.d);
        }

        // ------------------------------------------------------------------ garden
        static void BuildGarden(Layout layout)
        {
            var garden = layout.rooms.FirstOrDefault(r => r.outdoor);
            if (garden == null) return;
            float depth = garden.z1 - garden.z0, midZ = (garden.z0 + garden.z1) / 2;
            var zones = layout.garden_zones ?? new[] { new Zone { id = "terrace", material = "tiles", color = "#8E8A84", x0 = garden.x0, x1 = garden.x1 } };
            foreach (var zone in zones)
            {
                float len = zone.x1 - zone.x0;
                var mat = zone.material == "grass"
                    ? Mat($"Apt_{zone.id}", Hex(zone.color), 0.05f, GrassTexture(), new Vector2(len, depth) / 1.5f)
                    : Mat($"Apt_{zone.id}", Hex(zone.color), 0.2f, TileTexture(), new Vector2(len, depth) / 0.5f);
                Box(zone.id, new Vector3((zone.x0 + zone.x1) / 2, -0.06f, midZ), new Vector3(len, 0.1f, depth), mat);
            }

            foreach (var e in layout.garden_edges ?? new Edge[0])
            {
                bool alongX = Mathf.Approximately(e.from[1], e.to[1]);
                float line = alongX ? e.from[1] : e.from[0];
                float a = alongX ? Mathf.Min(e.from[0], e.to[0]) : Mathf.Min(e.from[1], e.to[1]);
                float b = alongX ? Mathf.Max(e.from[0], e.to[0]) : Mathf.Max(e.from[1], e.to[1]);
                if (e.style == "posts") PostFence(e, alongX, line, a, b);
                else
                {
                    var mat = e.material == "brick"
                        ? Mat($"Apt_{e.id}", Hex(e.color), 0.08f, BrickTexture(), new Vector2(b - a, e.h)) // texture = 1 m
                        : Mat($"Apt_{e.id}", Hex(e.color), 0.1f);
                    WallSegment(e.id, alongX, line, a, b, 0f, e.h, 0.08f, mat);
                }
                // An invisible wall above low edges: the cat cannot jump out of the garden.
                if (e.h > 3f) continue;
                float mid = (a + b) / 2, len = b - a;
                var block = Box($"{e.id}_block", alongX ? new Vector3(mid, e.h + 2f, line) : new Vector3(line, e.h + 2f, mid),
                    alongX ? new Vector3(len, 4f, 0.1f) : new Vector3(0.1f, 4f, len), null);
                Object.DestroyImmediate(block.GetComponent<Renderer>());
            }
        }

        /// <summary>
        /// Neighbouring buildings as brick blocks with flat gravel roofs the cat can walk on (heights from the
        /// Flemish height model), roof props, and invisible walls at the edge of the playable roofs.
        /// </summary>
        static void BuildBuildings(Layout layout)
        {
            foreach (var b in layout.buildings ?? new Building[0])
            {
                var root = new GameObject(b.id).transform;
                root.SetParent(level, false);
                float w = b.x1 - b.x0, d = b.z1 - b.z0, cx = (b.x0 + b.x1) / 2, cz = (b.z0 + b.z1) / 2;
                const float roofT = 0.12f, coping = 0.06f;
                if (b.roof == "pitched") { PitchedHouse(root, b); continue; }
                var brick = b.material == "plaster"
                    ? Mat($"Apt_{b.id}", Hex(b.color), 0.12f, PlasterTexture(), new Vector2(w, b.h - b.base_y) / 3f)
                    : Mat($"Apt_{b.id}", Hex(b.color), 0.08f, BrickTexture(), new Vector2(w, b.h));
                var gravel = Mat($"Apt_{b.id}_roof", Hex(b.roof_color), 0.05f, PebbleTexture(), new Vector2(w, d) * 7f); // small stones
                var stone = Mat("Apt_Coping", new Color(0.72f, 0.7f, 0.66f), 0.2f);
                Box("Walls", new Vector3(cx, (b.base_y + b.h - roofT) / 2, cz), new Vector3(w, b.h - roofT - b.base_y, d), brick).transform.SetParent(root, true);
                Box("Roof", new Vector3(cx, b.h - roofT / 2, cz), new Vector3(w, roofT, d), gravel).transform.SetParent(root, true);
                // A thin stone coping along the edges: the roof line reads clearly from the garden.
                foreach (float z in new[] { b.z0, b.z1 })
                    Box("Coping", new Vector3(cx, b.h + coping / 2, z), new Vector3(w + 0.04f, coping, 0.12f), stone, collider: false).transform.SetParent(root, true);
                foreach (float x in new[] { b.x0, b.x1 })
                    Box("Coping", new Vector3(x, b.h + coping / 2, cz), new Vector3(0.12f, coping, d + 0.04f), stone, collider: false).transform.SetParent(root, true);
            }

            foreach (var p in layout.roof_props ?? new Prop[0])
            {
                // Stands on the highest roof under it.
                float baseY = (layout.buildings ?? new Building[0])
                    .Where(b => p.x > b.x0 && p.x < b.x1 && p.z > b.z0 && p.z < b.z1).Select(b => b.h).DefaultIfEmpty(0f).Max();
                var go = Box(p.id, new Vector3(p.x, baseY + p.h / 2, p.z), new Vector3(p.w, p.h, p.d), Mat($"Apt_{p.id}", Hex(p.color), 0.1f, BrickTexture(), new Vector2(p.w, p.h)));
                Box(p.id + "_cap", new Vector3(p.x, baseY + p.h + 0.03f, p.z), new Vector3(p.w + 0.08f, 0.06f, p.d + 0.08f), Mat("Apt_Coping", Color.gray, 0.2f), collider: false)
                    .transform.SetParent(go.transform, true);
            }

            // The rest of the block as scenery: plain boxes, muted plaster and brick colours, no colliders.
            var tones = new[] { "#B9A796", "#A8826C", "#C9BFB2", "#9C8C7E", "#B38F78" };
            int n = 0;
            foreach (var c in layout.context_buildings ?? new ContextBuilding[0])
            {
                var tone = Hex(tones[n % tones.Length]);
                var body = Box($"Context_{n}", new Vector3(c.x, c.h / 2, c.z), new Vector3(c.w, c.h, c.d),
                    Mat($"Apt_Context_{n % tones.Length}", tone, 0.08f, BrickTexture(), new Vector2(6f, 6f)), collider: false);
                body.transform.rotation = Quaternion.Euler(0f, c.yaw, 0f);
                var roof = Box($"Context_{n}_roof", new Vector3(c.x, c.h + 0.05f, c.z), new Vector3(c.w + 0.2f, 0.1f, c.d + 0.2f),
                    Mat("Apt_ContextRoof", new Color(0.33f, 0.31f, 0.3f), 0.1f), collider: false);
                roof.transform.rotation = body.transform.rotation;
                n++;
            }
            // Window panes on the scenery blocks, and on the faces of story buildings that ask for them.
            var facades = (layout.buildings ?? new Building[0]).Where(b => !string.IsNullOrEmpty(b.windows))
                .Select(b => new ContextBuilding { x = (b.x0 + b.x1) / 2, z = (b.z0 + b.z1) / 2, w = b.x1 - b.x0, d = b.z1 - b.z0, h = b.h, y0 = b.base_y, faces = b.windows });
            BuildWindows((layout.context_buildings ?? new ContextBuilding[0]).Concat(facades).ToArray());
            // Ground under the block (yards, streets), just below the garden; it has a hole for the river.
            var groundMat = Mat("Apt_Ground", new Color(0.42f, 0.41f, 0.38f), 0.1f);
            var river = layout.water;
            if (river == null)
                Box("Ground", new Vector3(0f, -0.13f, -50f), new Vector3(200f, 0.1f, 300f), groundMat);
            else
            {
                Box("Ground_East", new Vector3((river.x1 + 100f) / 2, -0.13f, -50f), new Vector3(100f - river.x1, 0.1f, 300f), groundMat);
                Box("Ground_West", new Vector3((river.x0 - 100f) / 2, -0.13f, -50f), new Vector3(river.x0 + 100f, 0.1f, 300f), groundMat);
                BuildRiver(river);
            }
            foreach (var g in layout.grounds ?? new Ground[0])
            {
                float gw = g.x1 - g.x0, gd = g.z1 - g.z0;
                var mat = g.material == "grass"
                    ? Mat($"Apt_{g.id}", Hex(g.color), 0.05f, GrassTexture(), new Vector2(gw, gd) / 1.5f)
                    : Mat($"Apt_{g.id}", Hex(g.color), 0.15f, TileTexture(), new Vector2(gw, gd) / 0.6f);
                // Thick: a quay wall down to the water.
                Box(g.id, new Vector3((g.x0 + g.x1) / 2, g.top - 0.9f, (g.z0 + g.z1) / 2), new Vector3(gw, 1.8f, gd), mat);
            }

            foreach (var e in layout.bounds ?? new Edge[0])
            {
                bool alongX = Mathf.Approximately(e.from[1], e.to[1]);
                float line = alongX ? e.from[1] : e.from[0];
                float a = alongX ? Mathf.Min(e.from[0], e.to[0]) : Mathf.Min(e.from[1], e.to[1]);
                float b = alongX ? Mathf.Max(e.from[0], e.to[0]) : Mathf.Max(e.from[1], e.to[1]);
                float mid = (a + b) / 2, len = b - a;
                var block = Box($"Bound_{e.id}", alongX ? new Vector3(mid, e.y0 + 7f, line) : new Vector3(line, e.y0 + 7f, mid),
                    alongX ? new Vector3(len, 14f, 0.1f) : new Vector3(0.1f, 14f, len), null);
                Object.DestroyImmediate(block.GetComponent<Renderer>());
            }
        }

        /// <summary>
        /// The neighbour's garden, left alone for months: long yellow-green grass, clumps of tall grass the
        /// cat walks through, brambles, an old wheelbarrow (the hedgehog nest), a fallen chair, a rusty barrel.
        /// </summary>
        static void BuildWildGarden(WildGarden w)
        {
            if (w == null || w.x1 <= w.x0) return;
            var root = new GameObject("WildGarden").transform;
            root.SetParent(level, false);
            float len = w.x1 - w.x0, depth = w.z1 - w.z0;
            Box("WildGround", new Vector3((w.x0 + w.x1) / 2, -0.06f, (w.z0 + w.z1) / 2), new Vector3(len, 0.1f, depth),
                Mat("Apt_WildGround", Hex(w.color), 0.05f, GrassTexture(), new Vector2(len, depth) / 1.2f)).transform.SetParent(root, true);

            // Tall grass: one mesh of thin blades in clumps (no collider: the cat walks through it).
            var rng = new System.Random(4);
            float Rand() => (float)rng.NextDouble();
            var verts = new List<Vector3>(); var normals = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int c = 0; c < w.grass_clumps; c++)
            {
                var centre = new Vector3(w.x0 + 0.4f + Rand() * (len - 0.8f), 0f, w.z0 + 0.4f + Rand() * (depth - 0.8f));
                int blades = 40 + rng.Next(30);
                for (int i = 0; i < blades; i++)
                {
                    float a = Rand() * Mathf.PI * 2f, r = 0.35f * Mathf.Sqrt(Rand());
                    var basePos = centre + new Vector3(Mathf.Cos(a) * r, -0.02f, Mathf.Sin(a) * r);
                    float h = 0.25f + 0.3f * Rand(), half = 0.012f + 0.01f * Rand();
                    var side = Quaternion.Euler(0f, Rand() * 180f, 0f) * Vector3.right;
                    var lean = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * h * (0.15f + 0.35f * Rand());
                    int b0 = verts.Count;
                    verts.Add(basePos - side * half); verts.Add(basePos + side * half); verts.Add(basePos + Vector3.up * h + lean);
                    var n = Vector3.Cross(side, Vector3.up).normalized;
                    for (int k = 0; k < 3; k++) normals.Add(Vector3.Lerp(n, Vector3.up, 0.5f).normalized);
                    float tone = (rng.Next(4) + 0.5f) / 4f;
                    uvs.Add(new Vector2(tone, 0f)); uvs.Add(new Vector2(tone, 0f)); uvs.Add(new Vector2(tone, 1f)); // uv.y: wind sway weight
                    tris.AddRange(new[] { b0, b0 + 2, b0 + 1 });
                }
            }
            var mesh = new Mesh { name = "WildGrass" };
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
            string meshPath = $"{PlaygroundBuilder.ArtDir}/Meshes/WildGrass.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(meshPath));
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var grass = new GameObject("TallGrass");
            grass.transform.SetParent(root, false);
            grass.AddComponent<MeshFilter>().sharedMesh = mesh;
            var grassMat = PlaygroundBuilder.LitMaterial("Apt_TallGrass", Color.white, 0.2f, MakeTexture("WildGrassColours.png", 4, (x, y, size) => x switch
            {
                0 => new Color(0.36f, 0.42f, 0.18f),
                1 => new Color(0.46f, 0.5f, 0.22f),
                2 => new Color(0.6f, 0.58f, 0.3f), // dry blades
                _ => new Color(0.3f, 0.38f, 0.16f),
            }));
            WindFoliage(grassMat, 0.07f, 0.4f);
            grass.AddComponent<MeshRenderer>().sharedMaterial = grassMat;

            // Brambles: dark, thorny clumps; low ones have no collider (the cat pushes through).
            var bramble = Mat("Apt_Bramble", new Color(0.17f, 0.25f, 0.12f), 0.15f);
            var bramble2 = Mat("Apt_Bramble2", new Color(0.24f, 0.3f, 0.14f), 0.15f);
            foreach (var b in w.bushes ?? new Spot[0])
                for (int i = 0; i < 4; i++)
                {
                    var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    blob.name = "Bramble";
                    Object.DestroyImmediate(blob.GetComponent<Collider>());
                    blob.transform.SetParent(root, false);
                    blob.transform.position = new Vector3(b.x + (Rand() - 0.5f) * b.r, b.r * (0.35f + 0.25f * Rand()), b.z + (Rand() - 0.5f) * b.r);
                    blob.transform.localScale = new Vector3(b.r * 1.3f, b.r * (0.9f + 0.4f * Rand()), b.r * 1.3f);
                    blob.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? bramble : bramble2;
                }

            var rust = Mat("Apt_Rust", new Color(0.45f, 0.24f, 0.13f), 0.25f);
            var oldWood = Mat("Apt_OldWood", new Color(0.42f, 0.36f, 0.28f), 0.1f);
            Transform nest = null;
            if (w.wheelbarrow != null)
            {
                // Upside down on two handles: the hedgehog nest is under it (leaves and moss).
                var wb = new GameObject("Wheelbarrow").transform;
                wb.SetParent(root, false);
                wb.SetPositionAndRotation(new Vector3(w.wheelbarrow.x, 0f, w.wheelbarrow.z), Quaternion.Euler(0f, w.wheelbarrow.yaw, 0f));
                Box("Tray", wb.TransformPoint(new Vector3(0f, 0.32f, 0f)), new Vector3(0.62f, 0.05f, 0.85f), rust).transform.SetParent(wb, true);
                foreach (float sx in new[] { -1f, 1f })
                {
                    var wall = Box("TraySide", wb.TransformPoint(new Vector3(sx * 0.31f, 0.2f, 0f)), new Vector3(0.04f, 0.22f, 0.85f), rust, collider: false);
                    wall.transform.SetParent(wb, true);
                    Box("Handle", wb.TransformPoint(new Vector3(sx * 0.22f, 0.12f, -0.6f)), new Vector3(0.04f, 0.04f, 0.6f), oldWood, collider: false).transform.SetParent(wb, true);
                }
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Wheel";
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheel.transform.SetParent(wb, false);
                wheel.transform.localPosition = new Vector3(0f, 0.18f, 0.55f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.36f, 0.03f, 0.36f);
                wheel.GetComponent<Renderer>().sharedMaterial = Mat("Apt_Tyre", new Color(0.08f, 0.08f, 0.08f), 0.3f);
                var leaves = Mat("Apt_NestLeaves", new Color(0.45f, 0.3f, 0.15f), 0.05f);
                for (int i = 0; i < 6; i++)
                {
                    var pile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(pile.GetComponent<Collider>());
                    pile.name = "Leaves";
                    pile.transform.SetParent(wb, false);
                    pile.transform.localPosition = new Vector3((Rand() - 0.5f) * 0.4f, 0.02f, (Rand() - 0.5f) * 0.5f);
                    pile.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);
                    pile.GetComponent<Renderer>().sharedMaterial = leaves;
                }
                for (int i = 0; i < 3; i++) // three hoglets
                {
                    var hog = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(hog.GetComponent<Collider>());
                    hog.name = "Hoglet";
                    hog.transform.SetParent(wb, false);
                    hog.transform.localPosition = new Vector3(-0.12f + i * 0.12f, 0.06f, 0.1f);
                    hog.transform.localScale = new Vector3(0.06f, 0.045f, 0.08f);
                    hog.GetComponent<Renderer>().sharedMaterial = Mat("Hedgehog_Spines", new Color(0.36f, 0.3f, 0.24f), 0.2f);
                }
                nest = new GameObject("Nest").transform;
                nest.SetParent(wb, false);
                nest.localPosition = new Vector3(0f, 0f, -0.15f);
            }
            if (w.chair != null) // a garden chair, fallen on its back
            {
                var ch = new GameObject("OldChair").transform;
                ch.SetParent(root, false);
                ch.SetPositionAndRotation(new Vector3(w.chair.x, 0f, w.chair.z), Quaternion.Euler(0f, w.chair.yaw, 0f));
                var white = Mat("Apt_OldPlastic", new Color(0.78f, 0.77f, 0.72f), 0.3f);
                Box("Seat", ch.TransformPoint(new Vector3(0f, 0.22f, 0f)), new Vector3(0.45f, 0.04f, 0.45f), white).transform.SetParent(ch, true);
                var back = Box("Back", ch.TransformPoint(new Vector3(0f, 0.04f, -0.42f)), new Vector3(0.45f, 0.04f, 0.5f), white);
                back.transform.SetParent(ch, true);
                foreach (var leg in new[] { new Vector3(-0.2f, 0.1f, 0.2f), new Vector3(0.2f, 0.1f, 0.2f) })
                    Box("Leg", ch.TransformPoint(leg), new Vector3(0.03f, 0.22f, 0.03f), white, collider: false).transform.SetParent(ch, true);
            }
            if (w.barrel != null)
            {
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "RustyBarrel";
                barrel.transform.SetParent(root, false);
                barrel.transform.position = new Vector3(w.barrel.x, 0.45f, w.barrel.z);
                barrel.transform.localScale = new Vector3(0.58f, 0.45f, 0.58f);
                barrel.GetComponent<Renderer>().sharedMaterial = rust;
            }

            if (w.hedgehog != null) HedgehogModel(root, w.hedgehog, nest);
        }

        static void HedgehogModel(Transform parent, Spot spot, Transform nest)
        {
            var root = new GameObject("Hedgehog");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(new Vector3(spot.x, 0f, spot.z), Quaternion.Euler(0f, spot.yaw, 0f));
            var spines = Mat("Hedgehog_Spines", new Color(0.36f, 0.3f, 0.24f), 0.2f);
            var tips = Mat("Hedgehog_Tips", new Color(0.78f, 0.72f, 0.62f), 0.2f);
            var face = Mat("Hedgehog_Face", new Color(0.62f, 0.5f, 0.38f), 0.3f);
            var black = Mat("Mouse_Eye", new Color(0.02f, 0.02f, 0.02f), 0.9f);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            Transform Part(Transform p, Vector3 pos, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(p, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go.transform;
            }
            Part(body, new Vector3(0f, 0.065f, 0f), new Vector3(0.17f, 0.12f, 0.22f), spines);
            var rng = new System.Random(9);
            for (int i = 0; i < 26; i++) // spine tufts: light tips over the back
            {
                float a = (float)rng.NextDouble() * Mathf.PI, z = ((float)rng.NextDouble() - 0.5f) * 0.18f;
                Part(body, new Vector3(Mathf.Cos(a) * 0.075f, 0.065f + Mathf.Sin(a) * 0.055f, z), new Vector3(0.03f, 0.03f, 0.05f), i % 3 == 0 ? tips : spines);
            }
            var snout = new GameObject("Snout").transform;
            snout.SetParent(body, false);
            Part(snout, new Vector3(0f, 0.045f, 0.11f), new Vector3(0.07f, 0.06f, 0.08f), face);
            Part(snout, new Vector3(0f, 0.045f, 0.155f), Vector3.one * 0.018f, black);
            foreach (float sx in new[] { -1f, 1f })
                Part(snout, new Vector3(sx * 0.025f, 0.065f, 0.12f), Vector3.one * 0.012f, black);
            root.AddComponent<Hedgehog>().Configure(body, snout, nest);
        }

        /// <summary>The river: a dark green water surface (no collider) and a respawn on the quay for a cat that falls in.</summary>
        static void BuildRiver(Water w)
        {
            var water = Box("River", new Vector3((w.x0 + w.x1) / 2, w.top - 0.05f, (w.z0 + w.z1) / 2),
                new Vector3(w.x1 - w.x0, 0.1f, w.z1 - w.z0),
                Mat("Apt_River", Hex(w.color), 0.62f, RippleTexture(), new Vector2((w.x1 - w.x0) / 4f, (w.z1 - w.z0) / 4f)), collider: false);
            water.AddComponent<WaterRipples>();
            water.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var respawn = water.AddComponent<WaterRespawn>();
            respawn.Configure(new Rect(w.x0, w.z0, w.x1 - w.x0, w.z1 - w.z0), w.top);
        }

        /// <summary>Footbridges over the river: a stone deck with an arch below and railings.</summary>
        static void BuildBridges(Layout layout)
        {
            foreach (var b in layout.bridges ?? new Ground[0])
            {
                var root = new GameObject(b.id).transform;
                root.SetParent(level, false);
                float len = b.x1 - b.x0, wid = b.z1 - b.z0, cx = (b.x0 + b.x1) / 2, cz = (b.z0 + b.z1) / 2;
                var stone = Mat($"Apt_{b.id}", Hex(b.color), 0.2f, TileTexture(), new Vector2(len, wid) / 0.8f);
                var iron = Mat("Apt_Railing", new Color(0.16f, 0.2f, 0.18f), 0.5f);
                Box("Deck", new Vector3(cx, b.top - 0.15f, cz), new Vector3(len + 1f, 0.3f, wid), stone).transform.SetParent(root, true);
                var arch = Box("Arch", new Vector3(cx, b.top - 0.75f, cz), new Vector3(len * 0.75f, 0.9f, wid - 0.2f), stone, collider: false);
                arch.transform.SetParent(root, true);
                foreach (float side in new[] { -1f, 1f })
                {
                    float z = cz + side * (wid / 2 - 0.05f);
                    Box("Rail", new Vector3(cx, b.top + 0.85f, z), new Vector3(len + 1f, 0.06f, 0.06f), iron).transform.SetParent(root, true);
                    Box("RailCollider", new Vector3(cx, b.top + 0.45f, z), new Vector3(len + 1f, 0.9f, 0.1f), null).transform.SetParent(root, true);
                    for (float x = b.x0; x <= b.x1 + 0.01f; x += 1.3f)
                        Box("Baluster", new Vector3(x, b.top + 0.42f, z), new Vector3(0.04f, 0.84f, 0.04f), iron, collider: false).transform.SetParent(root, true);
                }
                foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial == null) Object.DestroyImmediate(r);
            }
        }

        /// <summary>
        /// The bakery on the far bank: a shopfront with a striped awning, bread crates, a bin, mouse holes
        /// in the wall and the bakery mouse (chapter 6).
        /// </summary>
        static void BuildBakery(Bakery b)
        {
            if (b == null) return;
            var root = new GameObject("BakeryFront").transform;
            root.SetParent(level, false);
            float cz = (b.z0 + b.z1) / 2, len = b.z1 - b.z0;
            var red = Mat("Apt_Awning", Hex(b.awning_color), 0.2f);
            var cream = Mat("Apt_AwningCream", new Color(0.93f, 0.9f, 0.82f), 0.2f);
            for (int i = 0; i < 12; i++) // striped awning, sloping out over the pavement
            {
                float z = b.z0 + 2f + (len - 4f) * (i + 0.5f) / 12;
                var stripe = Box("Awning", new Vector3(b.x + 0.7f, 3.0f, z), new Vector3(1.5f, 0.04f, (len - 4f) / 12), i % 2 == 0 ? red : cream, collider: false);
                stripe.transform.rotation = Quaternion.Euler(0f, 0f, -18f);
                stripe.transform.SetParent(root, true);
            }
            var window = Mat("Apt_ShopWindow", new Color(0.55f, 0.62f, 0.62f), 0.9f);
            Box("ShopWindow", new Vector3(b.x + 0.03f, 1.5f, cz + 3f), new Vector3(0.05f, 1.6f, 5f), window, collider: false).transform.SetParent(root, true);
            var bread = Mat("Apt_Bread", new Color(0.78f, 0.55f, 0.28f), 0.3f);
            for (int i = 0; i < 6; i++) // loaves in the window
            {
                var loaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(loaf.GetComponent<Collider>());
                loaf.transform.SetParent(root, false);
                loaf.transform.position = new Vector3(b.x - 0.15f, 1.0f, cz + 1f + i * 0.75f);
                loaf.transform.localScale = new Vector3(0.22f, 0.14f, 0.4f);
                loaf.GetComponent<Renderer>().sharedMaterial = bread;
            }
            var sign = GameObject.CreatePrimitive(PrimitiveType.Sphere); // a golden pretzel-like sign above the door
            Object.DestroyImmediate(sign.GetComponent<Collider>());
            sign.transform.SetParent(root, false);
            sign.transform.position = new Vector3(b.x + 0.5f, 3.6f, cz - 3f);
            sign.transform.localScale = new Vector3(0.1f, 0.6f, 0.8f);
            sign.GetComponent<Renderer>().sharedMaterial = Mat("Apt_Gold", new Color(0.85f, 0.65f, 0.2f), 0.7f);
            var wood = Mat("Apt_CrateWood", new Color(0.62f, 0.48f, 0.3f), 0.15f);
            foreach (var c in b.crates ?? new Spot[0])
                Box("BreadCrate", new Vector3(c.x, 0.2f, c.z), new Vector3(0.5f, 0.4f, 0.7f), wood).transform.SetParent(root, true);
            var binMat = Mat("Apt_Bin", new Color(0.18f, 0.3f, 0.22f), 0.3f);
            foreach (var c in b.bins ?? new Spot[0])
                Box("Bin", new Vector3(c.x, 0.55f, c.z), new Vector3(0.6f, 1.1f, 0.7f), binMat).transform.SetParent(root, true);
            if (b.marker != null && b.marker.Length == 2)
            {
                var m = new GameObject("Marker_Bakery");
                m.transform.SetParent(level, false);
                m.transform.position = new Vector3(b.marker[0], 0f, b.marker[1]);
            }
            if (b.mouse_holes != null && b.mouse_holes.Length > 0)
                BuildMouseTerritory(new MouseTerritory { id = "BakeryMouse", name = "Bakery mouse", bounty = 15, holes = b.mouse_holes });
        }

        /// <summary>Mouse holes in walls and one mouse that lives there (a bounty on the board).</summary>
        static Mouse BuildMouseTerritory(MouseTerritory t)
        {
            var holes = new List<Transform>();
            var dark = Mat("Apt_MouseHole", new Color(0.03f, 0.025f, 0.02f), 0f);
            foreach (var h in t.holes)
            {
                var hole = new GameObject(h.id).transform;
                hole.SetParent(level, false);
                hole.SetPositionAndRotation(new Vector3(h.x, h.y, h.z), Quaternion.Euler(0f, FacingYaw(h.facing) + 180f, 0f));
                var arch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(arch.GetComponent<Collider>());
                arch.transform.SetParent(hole, false);
                arch.transform.localPosition = new Vector3(0f, 0f, 0.004f);
                arch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                arch.transform.localScale = new Vector3(0.09f, 0.003f, 0.11f);
                arch.GetComponent<Renderer>().sharedMaterial = dark;
                holes.Add(hole);
            }
            var mouse = MouseModel(holes);
            mouse.name = t.id;
            mouse.SetBounty(t.name, t.bounty);
            return mouse;
        }

        /// <summary>Feathers to collect, floating a little above the place where they lie.</summary>
        static void BuildFeathers(Layout layout)
        {
            var white = Mat("Feather_White", new Color(0.95f, 0.94f, 0.9f), 0.3f);
            var tip = Mat("Feather_Tip", new Color(0.25f, 0.22f, 0.2f), 0.3f);
            white.EnableKeyword("_EMISSION");
            white.SetColor("_EmissionColor", new Color(0.35f, 0.33f, 0.28f));
            EditorUtility.SetDirty(white);
            foreach (var f in layout.feathers ?? new Feather[0])
            {
                var root = new GameObject($"Feather_{f.id}");
                root.transform.SetParent(level, false);
                root.transform.position = new Vector3(f.x, f.y + 0.2f, f.z);
                var visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
                var vane = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(vane.GetComponent<Collider>());
                vane.transform.SetParent(visual, false);
                vane.transform.localScale = new Vector3(0.05f, 0.012f, 0.2f);
                vane.GetComponent<Renderer>().sharedMaterial = white;
                var end = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(end.GetComponent<Collider>());
                end.transform.SetParent(visual, false);
                end.transform.localPosition = new Vector3(0f, 0f, 0.085f);
                end.transform.localScale = new Vector3(0.035f, 0.013f, 0.05f);
                end.GetComponent<Renderer>().sharedMaterial = tip;
                root.AddComponent<Collectable>().Configure(f.id, visual);
            }
        }

        /// <summary>Mallards on the river: drakes (green head) and brown ducks, in small groups.</summary>
        static void BuildDucks(Layout layout)
        {
            var w = layout.water;
            if (w == null || layout.ducks == null) return;
            var rng = new System.Random(8);
            var orange = Mat("Duck_Beak", new Color(0.95f, 0.62f, 0.12f), 0.4f);
            var brown = Mat("Duck_Brown", new Color(0.45f, 0.33f, 0.22f), 0.2f);
            var grey = Mat("Duck_Grey", new Color(0.66f, 0.64f, 0.6f), 0.25f);
            var green = Mat("Duck_Green", new Color(0.08f, 0.32f, 0.2f), 0.7f);
            var white = Mat("Duck_White", new Color(0.9f, 0.9f, 0.88f), 0.3f);
            var dark = Mat("Mouse_Eye", new Color(0.02f, 0.02f, 0.02f), 0.9f);
            int n = 0;
            foreach (var group in layout.ducks)
                for (int i = 0; i < Mathf.Max(1, group.count); i++)
                {
                    bool drake = i % 2 == 0;
                    var root = new GameObject($"Duck_{n++}");
                    root.transform.SetParent(level, false);
                    root.transform.SetPositionAndRotation(
                        new Vector3(group.x + ((float)rng.NextDouble() - 0.5f) * 3f, w.top, group.z + ((float)rng.NextDouble() - 0.5f) * 4f),
                        Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                    Transform Part(Transform parent, Vector3 pos, Vector3 scale, Material m, PrimitiveType type = PrimitiveType.Sphere)
                    {
                        var go = GameObject.CreatePrimitive(type);
                        Object.DestroyImmediate(go.GetComponent<Collider>());
                        go.transform.SetParent(parent, false);
                        go.transform.localPosition = pos;
                        go.transform.localScale = scale;
                        go.GetComponent<Renderer>().sharedMaterial = m;
                        return go.transform;
                    }
                    var body = new GameObject("Body").transform;
                    body.SetParent(root.transform, false);
                    Part(body, new Vector3(0f, 0.06f, 0f), new Vector3(0.2f, 0.14f, 0.36f), drake ? grey : brown);
                    Part(body, new Vector3(0f, 0.1f, -0.19f), new Vector3(0.1f, 0.06f, 0.12f), drake ? dark : brown); // tail
                    var head = new GameObject("Head").transform;
                    head.SetParent(body, false);
                    head.localPosition = new Vector3(0f, 0.17f, 0.15f);
                    Part(head, Vector3.zero, new Vector3(0.11f, 0.11f, 0.13f), drake ? green : brown);
                    if (drake) Part(head, new Vector3(0f, -0.06f, 0.01f), new Vector3(0.1f, 0.025f, 0.1f), white); // white collar
                    Part(head, new Vector3(0f, -0.01f, 0.09f), new Vector3(0.05f, 0.022f, 0.08f), orange, PrimitiveType.Cube);
                    foreach (float sx in new[] { -1f, 1f })
                        Part(head, new Vector3(sx * 0.045f, 0.02f, 0.04f), Vector3.one * 0.016f, dark);
                    root.AddComponent<Duck>().Configure(body, head, new Rect(w.x0, w.z0, w.x1 - w.x0, w.z1 - w.z0), w.top);
                }
        }

        /// <summary>
        /// The begijnhof from real data (GRB footprints, DHM II heights, listed trees), turned to lie along the
        /// river: houses with pitched roofs, the church, the boundary wall, the lindens and the old trees.
        /// Everything here is static scenery (batched), except the lindens the cat can climb.
        /// </summary>
        static void BuildBegijnhof(Begijnhof bg)
        {
            if (bg == null || bg.houses == null) return;
            var root = new GameObject("Begijnhof").transform;
            root.SetParent(level, false);
            var plaster = new[] { "#EFEAE0", "#E9E1D2", "#F3EFE6" };
            var bricks = new[] { "#A8644C", "#B5735A" };
            var roofs = new[] { "#9A4A32", "#8F4530", "#5A4E48" };
            int i = 0;
            foreach (var h in bg.houses)
            {
                var house = new GameObject(h.church ? "Church" : $"House_{i}").transform;
                house.SetParent(root, false);
                bool brick = h.church || i % 5 < 2;
                var b = new Building
                {
                    id = h.church ? "Beg_Church" : brick ? $"Beg_Brick_{i % bricks.Length}" : $"Beg_Plaster_{i % plaster.Length}",
                    color = h.church ? "#9C6A55" : brick ? bricks[i % bricks.Length] : plaster[i % plaster.Length],
                    roof_color = h.church ? "#4E4A48" : roofs[i % 2],
                    material = brick ? "brick" : "plaster",
                    x0 = -h.w / 2, x1 = h.w / 2, z0 = -h.d / 2, z1 = h.d / 2, h = h.h,
                    ridge = h.ridge > 1f ? h.ridge : Mathf.Min(h.d, h.w) * 0.35f,
                    front = h.front,
                };
                PitchedHouse(house, b); // built around the origin, then moved and turned
                house.SetPositionAndRotation(new Vector3(h.x, 0f, h.z), Quaternion.Euler(0f, h.yaw, 0f));
                i++;
            }
            var wallMat = Mat("Beg_Wall", Hex("#E8E2D6"), 0.12f);
            var copingMat = Mat("Apt_Coping", new Color(0.72f, 0.7f, 0.66f), 0.2f);
            foreach (var w in bg.walls ?? new Seg[0])
            {
                var a = new Vector3(w.x0, 0f, w.z0); var e = new Vector3(w.x1, 0f, w.z1);
                float len = Vector3.Distance(a, e);
                var rot = Quaternion.LookRotation((e - a).normalized);
                var wall = Box("BegWall", (a + e) / 2 + Vector3.up * 1.3f, new Vector3(0.4f, 2.6f, len + 0.4f), wallMat);
                wall.transform.rotation = rot;
                wall.transform.SetParent(root, true);
                var cap = Box("Coping", (a + e) / 2 + Vector3.up * 2.63f, new Vector3(0.5f, 0.06f, len + 0.5f), copingMat, collider: false);
                cap.transform.rotation = rot;
                cap.transform.SetParent(root, true);
            }
            foreach (var w in bg.blocks ?? new Seg[0]) // invisible: the cat stays inside (or on the wall)
            {
                var a = new Vector3(w.x0, 0f, w.z0); var e = new Vector3(w.x1, 0f, w.z1);
                var block = Box("BegBound", (a + e) / 2 + Vector3.up * 7f, new Vector3(0.1f, 14f, Vector3.Distance(a, e) + 0.5f), null);
                block.transform.rotation = Quaternion.LookRotation((e - a).normalized);
                Object.DestroyImmediate(block.GetComponent<Renderer>());
            }
            var bark = Mat("Beg_Bark", new Color(0.33f, 0.28f, 0.24f), 0.1f);
            var leafColours = new Dictionary<string, Color>
            {
                { "beech", new Color(0.33f, 0.16f, 0.17f) },   // copper beech (weeping)
                { "chestnut", new Color(0.24f, 0.42f, 0.16f) },
                { "maple", new Color(0.3f, 0.47f, 0.18f) },
                { "tree", new Color(0.27f, 0.44f, 0.2f) },
            };
            var rng = new System.Random(21);
            foreach (var t in bg.trees ?? new BegTree[0])
            {
                var tree = new GameObject($"Tree_{t.kind}").transform;
                tree.SetParent(root, false);
                tree.position = new Vector3(t.x, 0f, t.z);
                float height = Mathf.Clamp(t.h, 7f, 20f), crownR = height * 0.32f;
                bool weeping = t.kind == "beech";
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.transform.SetParent(tree, false);
                trunk.transform.localPosition = Vector3.up * height * 0.3f;
                trunk.transform.localScale = new Vector3(0.45f, height * 0.3f, 0.45f);
                trunk.GetComponent<Renderer>().sharedMaterial = bark;
                var leaves = Mat($"Beg_Leaves_{t.kind}", leafColours.TryGetValue(t.kind, out var lc) ? lc : leafColours["tree"], 0.1f);
                for (int k = 0; k < 5; k++)
                {
                    var blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(blob.GetComponent<Collider>());
                    blob.transform.SetParent(tree, false);
                    float a = k * 1.26f + (float)rng.NextDouble();
                    blob.transform.localPosition = new Vector3(Mathf.Cos(a) * crownR * 0.45f, height * (weeping ? 0.55f : 0.68f) + (k == 0 ? crownR * 0.3f : 0f), Mathf.Sin(a) * crownR * 0.45f);
                    blob.transform.localScale = weeping ? new Vector3(crownR * 1.2f, crownR * 1.6f, crownR * 1.2f) : Vector3.one * crownR * (1.1f + 0.3f * (float)rng.NextDouble());
                    blob.GetComponent<Renderer>().sharedMaterial = leaves;
                }
            }
            if (bg.church_door != null && bg.church_door.Length == 2)
            {
                var marker = new GameObject("Marker_ChurchDoor");
                marker.transform.SetParent(level, false);
                marker.transform.position = new Vector3(bg.church_door[0], 0f, bg.church_door[1]);
            }
            if (bg.exit != null && bg.exit.barrel != null && bg.exit.barrel.h > 0f) // a rain barrel by the garden shed (the shed is a climbable)
            {
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = "RainBarrel";
                barrel.transform.SetParent(root, false);
                barrel.transform.position = new Vector3(bg.exit.barrel.x, bg.exit.barrel.h / 2, bg.exit.barrel.z);
                barrel.transform.localScale = new Vector3(bg.exit.barrel.r * 2, bg.exit.barrel.h / 2, bg.exit.barrel.r * 2);
                barrel.GetComponent<Renderer>().sharedMaterial = Mat("Beg_Barrel", new Color(0.2f, 0.28f, 0.22f), 0.35f);
            }
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(child.gameObject, StaticEditorFlags.BatchingStatic);

            var church = bg.houses.FirstOrDefault(x => x.church);
            if (church != null)
            {
                var bells = new GameObject("ChurchBells");
                bells.transform.SetParent(level, false);
                bells.transform.position = new Vector3(church.x, church.h + church.ridge, church.z);
                bells.AddComponent<AudioSource>().playOnAwake = false;
                bells.AddComponent<ChurchBells>();
            }
            if (bg.cat != null) // the old grey cat of the begijnhof, on a stone step
            {
                Box("CatStep", new Vector3(bg.cat.x, bg.cat.step / 2, bg.cat.z), new Vector3(0.9f, bg.cat.step, 0.7f), copingMat).transform.SetParent(root, true);
                var npc = PlaygroundBuilder.NpcCatModel("GreyCat", level, new Color(0.46f, 0.47f, 0.5f), 1.05f);
                npc.transform.SetPositionAndRotation(new Vector3(bg.cat.x, bg.cat.step, bg.cat.z), Quaternion.Euler(0f, bg.cat.yaw, 0f));
                npc.AddComponent<NpcCat>().Configure(npc.GetComponentInChildren<Animator>());
            }

            int n = 0;
            foreach (var l in bg.lindens ?? new BegTree[0]) // pollarded lindens: climbable, not static
                BuildClimbable(new ClimbableInfo
                {
                    id = $"linden_{++n}", type = "linden", x = l.x, z = l.z, radius = 0.16f, h = 2.6f, climb_top = 2.2f,
                    color = "#5E5248", top_color = "#5C8A35",
                });
        }

        /// <summary>A house with a pitched roof (ridge along the long side), gables, a door and windows on its front.</summary>
        static void PitchedHouse(Transform root, Building b)
        {
            float w = b.x1 - b.x0, d = b.z1 - b.z0, cx = (b.x0 + b.x1) / 2, cz = (b.z0 + b.z1) / 2;
            var wallMat = b.material == "plaster" ? Mat($"Apt_{b.id}", Hex(b.color), 0.12f, PlasterTexture(), new Vector2(w, b.h) / 3f) : Mat($"Apt_{b.id}", Hex(b.color), 0.08f, BrickTexture(), new Vector2(w, b.h));
            Box("Walls", new Vector3(cx, b.h / 2, cz), new Vector3(w, b.h, d), wallMat).transform.SetParent(root, true);
            bool alongX = b.ridge_along == "x" || (b.ridge_along != "z" && w >= d); // the ridge runs along the long side unless set
            float span = alongX ? d : w, length = alongX ? w : d, ridge = b.ridge > 0f ? b.ridge : span * 0.4f;
            float slope = Mathf.Atan2(ridge, span / 2) * Mathf.Rad2Deg, slab = Mathf.Sqrt(span * span / 4 + ridge * ridge) + 0.3f;
            // Rows of slates (1 m of texture = 4 rows), parallel to the ridge: the cube's top face maps x to u, z to v.
            var tiles = alongX
                ? Mat($"Apt_{b.id}_tiles", Hex(b.roof_color), 0.25f, RoofTexture(false), new Vector2(length + 0.3f, slab))
                : Mat($"Apt_{b.id}_tiles", Hex(b.roof_color), 0.25f, RoofTexture(true), new Vector2(slab, length + 0.3f));
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 across = alongX ? Vector3.forward : Vector3.right;
                var roof = Box("RoofSide", new Vector3(cx, b.h + ridge / 2, cz) + across * side * span / 4,
                    alongX ? new Vector3(length + 0.3f, 0.12f, slab) : new Vector3(slab, 0.12f, length + 0.3f), tiles);
                roof.transform.rotation = alongX ? Quaternion.Euler(side * slope, 0f, 0f) : Quaternion.Euler(0f, 0f, -side * slope);
                roof.transform.SetParent(root, true);
            }
            // Trims: a ridge cap and zinc gutters along both eaves.
            var cap = Mat($"Apt_{b.id}_cap", Hex(b.roof_color) * 0.7f, 0.2f);
            var zinc = Mat("Apt_Zinc", new Color(0.55f, 0.57f, 0.58f), 0.55f);
            Box("RidgeCap", new Vector3(cx, b.h + ridge + 0.03f, cz), alongX ? new Vector3(length + 0.3f, 0.1f, 0.24f) : new Vector3(0.24f, 0.1f, length + 0.3f), cap, collider: false)
                .transform.SetParent(root, true);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 across = alongX ? Vector3.forward : Vector3.right;
                Box("Gutter", new Vector3(cx, b.h - 0.04f, cz) + across * side * (span / 2 + 0.14f),
                    alongX ? new Vector3(length + 0.3f, 0.1f, 0.13f) : new Vector3(0.13f, 0.1f, length + 0.3f), zinc, collider: false).transform.SetParent(root, true);
            }
            // Gables: a cube turned 45 degrees, squeezed to fit the roof slope (its lower half hides in the walls).
            foreach (float end in new[] { -1f, 1f })
            {
                var gable = new GameObject("Gable").transform;
                gable.SetParent(root, false);
                Vector3 along = alongX ? Vector3.right : Vector3.forward;
                gable.position = new Vector3(cx, b.h, cz) + along * end * (length / 2 - 0.06f);
                gable.rotation = alongX ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
                gable.localScale = new Vector3(span / 2 / ridge, 1f, 1f);
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.transform.SetParent(gable, false);
                cube.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                cube.transform.localScale = new Vector3(ridge * Mathf.Sqrt(2f), ridge * Mathf.Sqrt(2f), 0.1f);
                cube.GetComponent<Renderer>().sharedMaterial = wallMat;
            }
            if (string.IsNullOrEmpty(b.front)) return;
            // Front: doors and windows along the face.
            Vector3 n = b.front == "+x" ? Vector3.right : b.front == "-x" ? Vector3.left : b.front == "+z" ? Vector3.forward : Vector3.back;
            bool faceAlongX = n.z != 0f;
            float faceLen = faceAlongX ? w : d;
            Vector3 faceCentre = new Vector3(cx, 0f, cz) + Vector3.Scale(n, new Vector3(w / 2, 0f, d / 2));
            Vector3 tangent = faceAlongX ? Vector3.right : Vector3.forward;
            var door = Mat("Apt_BegDoor", new Color(0.45f, 0.12f, 0.1f), 0.3f);
            var glass = Mat("Apt_BegWindow", new Color(0.12f, 0.14f, 0.16f), 0.8f);
            int bays = Mathf.Max(1, Mathf.FloorToInt(faceLen / 5f));
            for (int i = 0; i < bays; i++)
            {
                float t = -faceLen / 2 + faceLen * (i + 0.5f) / bays;
                Vector3 at = faceCentre + tangent * t + n * 0.03f;
                Box("Door", at + Vector3.up * 1.0f + tangent * -0.9f, faceAlongX ? new Vector3(0.95f, 2f, 0.08f) : new Vector3(0.08f, 2f, 0.95f), door, collider: false).transform.SetParent(root, true);
                Box("Window", at + Vector3.up * 1.5f + tangent * 0.8f, faceAlongX ? new Vector3(1.0f, 1.2f, 0.06f) : new Vector3(0.06f, 1.2f, 1.0f), glass, collider: false).transform.SetParent(root, true);
            }
        }

        /// <summary>Stone slabs in a line over the lawn, with a strip of grass between them.</summary>
        static void BuildPaths(Layout layout)
        {
            var rng = new System.Random(3);
            foreach (var pa in layout.garden_paths ?? new GardenPath[0])
            {
                var root = new GameObject(pa.id).transform;
                root.SetParent(level, false);
                var stone = Mat($"Apt_{pa.id}", Hex(pa.color), 0.15f);
                var stoneDark = Mat($"Apt_{pa.id}_2", Hex(pa.color) * 0.88f, 0.15f);
                Vector3 a = new Vector3(pa.from[0], 0f, pa.from[1]), b = new Vector3(pa.to[0], 0f, pa.to[1]);
                float length = Vector3.Distance(a, b), slab = pa.slab > 0f ? pa.slab : 0.6f;
                const float gap = 0.04f;
                int count = Mathf.Max(1, Mathf.FloorToInt(length / (slab + gap)));
                Quaternion along = Quaternion.LookRotation((b - a).normalized);
                for (int i = 0; i < count; i++)
                {
                    // Flush with the lawn (its top is at -0.01); no collider: the lawn carries the cat.
                    Vector3 c = a + (b - a) * ((i + 0.5f) / count) + Vector3.up * -0.012f;
                    var go = Box("Slab", c, new Vector3(pa.w, 0.03f, slab), rng.Next(3) == 0 ? stoneDark : stone, collider: false);
                    go.transform.rotation = along * Quaternion.Euler(0f, ((float)rng.NextDouble() - 0.5f) * 3f, 0f);
                    go.transform.SetParent(root, true);
                }
            }
        }

        /// <summary>Concrete posts with horizontal wooden planks between them.</summary>
        static void PostFence(Edge e, bool alongX, float line, float a, float b)
        {
            var root = new GameObject(e.id).transform;
            root.SetParent(level, false);
            var post = Mat($"Apt_{e.id}_post", Hex(e.post_color), 0.15f);
            var plank = Mat($"Apt_{e.id}_plank", Hex(e.color), 0.1f);
            var plankDark = Mat($"Apt_{e.id}_plank2", Hex(e.color) * 0.85f, 0.1f);
            Vector3 At(float along, float y) => alongX ? new Vector3(along, y, line) : new Vector3(line, y, along);
            Vector3 Size(float along, float y, float across) => alongX ? new Vector3(along, y, across) : new Vector3(across, y, along);
            float spacing = e.post_spacing > 0f ? e.post_spacing : 2f;
            int posts = Mathf.Max(1, Mathf.RoundToInt((b - a) / spacing));
            for (int i = 0; i <= posts; i++)
                Box("Post", At(a + (b - a) * i / posts, (e.h + 0.1f) / 2), Size(0.12f, e.h + 0.1f, 0.12f), post, collider: false)
                    .transform.SetParent(root, true);
            const float board = 0.15f, gap = 0.035f;
            int rows = Mathf.FloorToInt(e.h / (board + gap));
            bool hasGap = e.gap_w > 0f;
            float g0 = e.gap_at - e.gap_w / 2, g1 = e.gap_at + e.gap_w / 2, holeTop = 0f;
            void Plank(float from, float to, float y, Material m) =>
                Box("Plank", At((from + to) / 2, y), Size(to - from, board, 0.03f), m, collider: false).transform.SetParent(root, true);
            for (int r = 0; r < rows; r++)
            {
                float bottom = 0.04f + r * (board + gap), y = bottom + board / 2;
                var m = r % 2 == 0 ? plank : plankDark;
                if (hasGap && bottom < e.gap_h)
                {
                    // Loose planks: the bottom rows have a cat-sized hole.
                    Plank(a, g0, y, m);
                    Plank(g1, b, y, m);
                    holeTop = bottom + board + gap;
                }
                else Plank(a, b, y, m);
            }
            void AddCollider(float from, float to, float y0, float y1)
            {
                var c = Box("Collider", At((from + to) / 2, (y0 + y1) / 2), Size(to - from, y1 - y0, 0.12f), null);
                Object.DestroyImmediate(c.GetComponent<Renderer>());
                c.transform.SetParent(root, true);
            }
            if (!hasGap) { AddCollider(a, b, 0f, e.h); return; }
            AddCollider(a, g0, 0f, e.h);
            AddCollider(g1, b, 0f, e.h);
            AddCollider(g0, g1, holeTop, e.h);
            // The loose plank lies in the grass on the far side.
            var fallen = Box("FallenPlank", At(e.gap_at + 0.1f, 0.02f) + (alongX ? new Vector3(0f, 0f, 0.35f) : new Vector3(0.35f, 0f, 0f)),
                Size(e.gap_w + 0.5f, 0.03f, board), plankDark, collider: false);
            fallen.transform.rotation = Quaternion.Euler(0f, 25f, 0f);
            fallen.transform.SetParent(root, true);
        }

        static void PicnicTable(Transform root, Prop p, Color color)
        {
            var wood = Mat("Apt_PicnicWood", color, 0.15f);
            var dark = Mat("Apt_PicnicWoodDark", color * 0.75f, 0.15f);
            Vector3 o = root.position;
            float topD = p.d * 0.5f, benchD = 0.26f, benchZ = p.d / 2 - benchD / 2;
            // Top and benches carry colliders (the cat can jump bench -> table); under the table is free.
            for (int i = 0; i < 5; i++) // top planks
                Box("TopPlank", o + new Vector3(0f, p.h - 0.02f, -topD / 2 + topD * (i + 0.5f) / 5), new Vector3(p.w, 0.04f, topD / 5 - 0.01f),
                    i % 2 == 0 ? wood : dark, collider: false).transform.SetParent(root, true);
            Box("TopCollider", o + new Vector3(0f, p.h - 0.02f, 0f), new Vector3(p.w, 0.04f, topD), null).transform.SetParent(root, true);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Bench", o + new Vector3(0f, 0.43f, side * benchZ), new Vector3(p.w, 0.04f, benchD), wood).transform.SetParent(root, true);
                foreach (float end in new[] { -1f, 1f }) // A-frame legs
                {
                    var leg = Box("Leg", o + new Vector3(end * (p.w / 2 - 0.2f), p.h / 2, side * topD * 0.45f),
                        new Vector3(0.06f, p.h * 1.08f, 0.06f), dark, collider: false);
                    leg.transform.rotation = Quaternion.Euler(side * 22f, 0f, 0f);
                    leg.transform.SetParent(root, true);
                }
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) if (r.sharedMaterial == null) Object.DestroyImmediate(r);
        }

        static void Firewood(Transform root, Prop p, Color color)
        {
            var bark = Mat("Apt_Bark", color, 0.05f);
            var barkDark = Mat("Apt_BarkDark", color * 0.7f, 0.05f);
            var cut = Mat("Apt_LogEnd", new Color(0.76f, 0.62f, 0.42f), 0.05f);
            Vector3 o = root.position;
            const float r = 0.065f;
            int rows = Mathf.FloorToInt(p.h / (2 * r * 0.9f)), perRow = Mathf.FloorToInt(p.d / (2 * r));
            var rng = new System.Random(7);
            for (int row = 0; row < rows; row++)
            for (int i = 0; i < perRow; i++)
            {
                float y = r + row * 2 * r * 0.9f;
                float z = -p.d / 2 + r + i * 2 * r + (row % 2) * r * 0.5f;
                float len = p.w * (0.85f + 0.15f * (float)rng.NextDouble());
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "Log";
                Object.DestroyImmediate(log.GetComponent<Collider>());
                log.transform.SetParent(root, false);
                log.transform.localPosition = new Vector3(((float)rng.NextDouble() - 0.5f) * 0.1f, y, z);
                log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                float rr = r * (0.8f + 0.3f * (float)rng.NextDouble());
                log.transform.localScale = new Vector3(rr * 2, len / 2, rr * 2);
                log.GetComponent<Renderer>().sharedMaterials = new[] { rng.Next(2) == 0 ? bark : barkDark };
            }
            // Cut ends: thin light discs on the visible ends of the stack.
            foreach (float end in new[] { -1f, 1f })
                Box("Ends", o + new Vector3(end * (p.w * 0.47f), p.h / 2, 0f), new Vector3(0.01f, p.h * 0.95f, p.d * 0.95f), cut, collider: false)
                    .transform.SetParent(root, true);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, p.h / 2, 0f);
            col.size = new Vector3(p.w, p.h, p.d);
        }

        static void BuildClimbable(ClimbableInfo c)
        {
            var root = new GameObject(c.id).transform;
            root.SetParent(level, false);
            root.position = new Vector3(c.x, 0f, c.z);
            var wood = Mat($"Apt_{c.id}", Hex(c.color), 0.15f);
            var top = Mat($"Apt_{c.id}_top", Hex(c.top_color), 0.1f);
            var climbable = root.gameObject.AddComponent<Climbable>();
            if (c.type == "ivy")
            {
                // Ivy on a wall that runs along X; it hangs from the top down to c.bottom.
                float h = c.h - c.bottom, z = c.z + 0.04f + c.d / 2; // in front of the wall face
                var slab = Box("IvyBody", new Vector3(c.x, c.bottom + h / 2, z), new Vector3(c.w, h, c.d), wood);
                slab.transform.SetParent(root, true);
                // The leaves: one mesh of small flat leaves (one draw call), facing out of the wall and a bit down.
                var leaves = new GameObject("Leaves");
                leaves.transform.SetParent(root, false);
                leaves.transform.position = new Vector3(c.x, 0f, z + c.d / 2);
                leaves.AddComponent<MeshFilter>().sharedMesh = IvyLeafMesh(c, h);
                var leafRenderer = leaves.AddComponent<MeshRenderer>();
                leafRenderer.sharedMaterial = IvyLeafMat();
                climbable.Configure(c.climb_top, c.standable);
                return;
            }
            if (c.type == "pipe")
            {
                // A drainpipe from c.bottom (a lower roof) to the top, with brackets.
                var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipe.name = "Pipe";
                pipe.transform.SetParent(root, false);
                float len = c.h - c.bottom;
                pipe.transform.localPosition = Vector3.up * (c.bottom + len / 2);
                pipe.transform.localScale = new Vector3(c.radius * 2, len / 2, c.radius * 2);
                pipe.GetComponent<Renderer>().sharedMaterial = Mat($"Apt_{c.id}", Hex(c.color), 0.5f);
                for (float y = c.bottom + 0.5f; y < c.h; y += 0.9f)
                    Box("Bracket", root.position + Vector3.up * y + (c.wall == "-x" ? Vector3.left : c.wall == "+x" ? Vector3.right : Vector3.back) * c.radius,
                        new Vector3(c.radius * 2.4f, 0.03f, c.radius * 2.4f),
                        Mat($"Apt_{c.id}", Hex(c.color), 0.5f), collider: false).transform.SetParent(root, true);
                climbable.Configure(c.climb_top, c.standable);
                return;
            }
            if (c.type == "linden")
            {
                // A pollarded linden (begijnhof): a trunk, a few knobbly arms, a flat clipped crown.
                var trunkL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunkL.name = "Trunk";
                trunkL.transform.SetParent(root, false);
                trunkL.transform.localPosition = Vector3.up * c.h / 2;
                trunkL.transform.localScale = new Vector3(c.radius * 2, c.h / 2, c.radius * 2);
                trunkL.GetComponent<Renderer>().sharedMaterial = wood;
                foreach (float a in new[] { 0f, 120f, 240f })
                {
                    var arm = Box("Arm", root.position + Vector3.up * (c.h + 0.25f) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 0.3f,
                        new Vector3(0.12f, 0.6f, 0.12f), wood, collider: false);
                    arm.transform.rotation = Quaternion.Euler(0f, a, 0f) * Quaternion.Euler(35f, 0f, 0f);
                    arm.transform.SetParent(root, true);
                }
                Box("Crown", root.position + Vector3.up * (c.h + 1.1f), new Vector3(2.2f, 1.1f, 2.2f), top, collider: false).transform.SetParent(root, true);
                climbable.Configure(c.climb_top, false);
                return;
            }
            if (c.type == "tree")
            {
                // A maple: a trunk the cat can climb to about fence height, a few branches, a wide crown.
                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Trunk";
                trunk.transform.SetParent(root, false);
                trunk.transform.localPosition = Vector3.up * c.h / 2;
                trunk.transform.localScale = new Vector3(c.radius * 2, c.h / 2, c.radius * 2);
                trunk.GetComponent<Renderer>().sharedMaterial = wood;
                var rng = new System.Random(5);
                float Rand() => (float)rng.NextDouble();
                for (int i = 0; i < 4; i++) // branches from the fork, no colliders
                {
                    var branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    branch.name = "Branch";
                    Object.DestroyImmediate(branch.GetComponent<Collider>());
                    branch.transform.SetParent(root, false);
                    Quaternion tilt = Quaternion.Euler(0f, i * 90f + 30f * Rand(), 0f) * Quaternion.Euler(30f + 15f * Rand(), 0f, 0f);
                    float len = 1.1f + 0.4f * Rand();
                    branch.transform.localRotation = tilt;
                    branch.transform.localPosition = Vector3.up * (c.h - 0.2f) + tilt * Vector3.up * len / 2;
                    branch.transform.localScale = new Vector3(c.radius, len / 2, c.radius);
                    branch.GetComponent<Renderer>().sharedMaterial = wood;
                }
                var leafDark = Mat($"Apt_{c.id}_top2", Hex(c.top_color) * 0.78f, 0.1f);
                for (int i = 0; i < 14; i++) // crown: flattened leaf clusters, no colliders
                {
                    var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    crown.name = "Leaves";
                    Object.DestroyImmediate(crown.GetComponent<Collider>());
                    crown.transform.SetParent(root, false);
                    float angle = Rand() * Mathf.PI * 2f, r = 1.4f * Mathf.Sqrt(Rand());
                    crown.transform.localPosition = new Vector3(Mathf.Cos(angle) * r, c.h + 0.8f + Rand() * 1.4f - r * 0.3f, Mathf.Sin(angle) * r);
                    float size = 1.1f + 0.6f * Rand();
                    crown.transform.localScale = new Vector3(size, size * 0.7f, size);
                    crown.GetComponent<Renderer>().sharedMaterial = rng.Next(2) == 0 ? top : leafDark;
                }
                climbable.Configure(c.climb_top, false);
                return;
            }

            // Shed: wooden body, a flat roof the cat can stand on, a door on the south side.
            Box("Body", root.position + Vector3.up * (c.h - 0.06f) / 2, new Vector3(c.w, c.h - 0.06f, c.d), wood)
                .transform.SetParent(root, true);
            Box("Roof", root.position + Vector3.up * (c.h - 0.03f), new Vector3(c.w, 0.06f, c.d), top)
                .transform.SetParent(root, true);
            Box("Door", root.position + new Vector3(0f, 0.8f, -c.d / 2 - 0.01f), new Vector3(0.7f, 1.55f, 0.02f),
                Mat("Apt_ShedDoor", Hex(c.color) * 0.7f, 0.2f), collider: false).transform.SetParent(root, true);
            for (int i = 1; i < 6; i++) // planks
                Box("Plank", root.position + new Vector3(0f, i * c.h / 6, -c.d / 2 - 0.005f), new Vector3(c.w, 0.015f, 0.01f),
                    Mat("Apt_ShedPlank", Hex(c.color) * 0.55f, 0.2f), collider: false).transform.SetParent(root, true);
            climbable.Configure(c.h, c.standable);
        }

        /// <summary>
        /// Ivy leaves as one mesh: many small pointed leaves, denser at the top, ragged at the bottom.
        /// Local space: x along the wall, y up, +z out of the wall. The colour comes from a 4-pixel strip.
        /// </summary>
        static Mesh IvyLeafMesh(ClimbableInfo c, float h)
        {
            var rng = new System.Random(11);
            float Rand() => (float)rng.NextDouble();
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int count = Mathf.RoundToInt(c.w * h * 70f);
            for (int i = 0; i < count; i++)
            {
                float v = Mathf.Pow(Rand(), 0.75f); // more leaves high up
                float y = c.bottom - 0.12f + v * (h + 0.2f);
                float spread = c.w * (0.7f + 0.3f * (y - c.bottom) / h);
                float x = (Rand() - 0.5f) * spread;
                float size = 0.06f + 0.07f * Rand();
                // Turn in the wall plane (Unity applies z first), then tilt the face up a little to catch the sun.
                float tilt = 15f + 30f * Rand();
                Quaternion rot = Quaternion.Euler(-tilt, 0f, (Rand() - 0.5f) * 140f);
                // Far enough from the wall that the tilted tip stays in front of it.
                Vector3 o = new Vector3(x, y, size * Mathf.Sin(tilt * Mathf.Deg2Rad) + 0.005f + 0.05f * Rand());
                Vector3 n = rot * Vector3.forward;
                float colour = (rng.Next(4) + 0.5f) / 4f;
                int b = verts.Count;
                // A pointed leaf: stem, two wide sides, tip.
                foreach (var p in new[] { new Vector3(0f, -0.15f, 0f), new Vector3(-0.5f, 0.35f, 0f), new Vector3(0f, 1f, 0f), new Vector3(0.5f, 0.35f, 0f) })
                {
                    Vector3 q = rot * (p * size);
                    verts.Add(o + q);
                    normals.Add(n);
                    uvs.Add(new Vector2(colour, p.y > 0.9f ? 1f : p.y > 0f ? 0.55f : 0f)); // uv.y: wind sway weight, the tip moves most
                }
                tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }
            var mesh = new Mesh { name = $"Ivy_{c.id}" };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            string path = $"{PlaygroundBuilder.ArtDir}/Meshes/Ivy_{c.id}.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Mesh lowSphere;

        /// <summary>
        /// A sphere with about 100 triangles (Unity's built-in one has 760), for small animals made of many parts.
        /// Radius 0.5, like the built-in sphere, so the parts keep their size.
        /// </summary>
        static Mesh LowSphere()
        {
            if (lowSphere != null) return lowSphere;
            const int lon = 10, lat = 6;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int j = 0; j <= lat; j++)
            for (int i = 0; i <= lon; i++)
            {
                float a = Mathf.PI * j / lat, b = 2f * Mathf.PI * i / lon;
                verts.Add(0.5f * new Vector3(Mathf.Sin(a) * Mathf.Cos(b), Mathf.Cos(a), Mathf.Sin(a) * Mathf.Sin(b)));
            }
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int v = j * (lon + 1) + i;
                if (j > 0) { tris.Add(v); tris.Add(v + 1); tris.Add(v + lon + 1); }
                if (j < lat - 1) { tris.Add(v + 1); tris.Add(v + lon + 2); tris.Add(v + lon + 1); }
            }
            var mesh = new Mesh { name = "LowSphere" };
            mesh.SetVertices(verts);
            mesh.SetNormals(verts.ConvertAll(x => x.normalized));
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            string path = $"{PlaygroundBuilder.ArtDir}/Meshes/LowSphere.asset";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return lowSphere = mesh;
        }

        static Material IvyLeafMat()
        {
            if (materials.TryGetValue("Apt_IvyLeaves", out var m)) return m;
            m = PlaygroundBuilder.LitMaterial("Apt_IvyLeaves", Color.white, 0.35f, IvyColours());
            WindFoliage(m, 0.012f, 0.5f);
            materials["Apt_IvyLeaves"] = m;
            return m;
        }

        /// <summary>
        /// Grass and leaves that move in the wind (Draho/WindFoliage, both faces lit). The mesh's uv.y is the sway weight.
        /// </summary>
        static void WindFoliage(Material m, float strength, float translucency)
        {
            var shader = Shader.Find("Draho/WindFoliage");
            if (shader == null) { Debug.LogWarning("Draho/WindFoliage not found, the foliage stays still"); m.SetFloat("_Cull", 0f); return; }
            m.shader = shader;
            m.SetFloat("_WindStrength", strength);
            m.SetFloat("_Translucency", translucency);
            m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
        }

        static Texture2D IvyColours() => MakeTexture("IvyColours.png", 4, (x, y, size) => x switch
        {
            0 => new Color(0.13f, 0.27f, 0.11f),
            1 => new Color(0.18f, 0.34f, 0.13f),
            2 => new Color(0.24f, 0.42f, 0.16f),
            _ => new Color(0.28f, 0.43f, 0.17f), // young leaves
        });

        static Texture2D BrickTexture() => MakeTexture("Bricks.png", 256, (x, y, size) =>
        {
            // 1 m square: 16 rows of 4 bricks (25 x 6.25 cm), every second row shifted by half a brick.
            const int rowH = 16, brickW = 64, mortar = 2;
            int row = y / rowH;
            int shifted = x + (row % 2) * brickW / 2;
            int col = (shifted / brickW) % (size / brickW);
            bool joint = y % rowH < mortar || shifted % brickW < mortar;
            if (joint) return new Color(0.78f, 0.76f, 0.72f);
            int hash = (row * 73856093) ^ (col * 19349663);
            float tone = 0.78f + 0.22f * (((hash >> 4) & 15) / 15f);
            float grain = 0.95f + 0.05f * (((x * 7919 + y * 104729) >> 3 & 7) / 7f);
            float soot = Mathf.Lerp(1f, 0.85f, Mathf.PerlinNoise(x * 0.02f, y * 0.02f)); // old, uneven wall
            float t = tone * grain * soot;
            return new Color(t, t * 0.93f, t * 0.88f);
        });

        // ------------------------------------------------------------------ light
        static void BuildLamps(Layout layout)
        {
            foreach (var l in layout.lamps ?? new Lamp[0])
            {
                var light = new GameObject(l.id).AddComponent<Light>();
                light.transform.SetParent(level, false);
                light.transform.position = new Vector3(l.x, l.y, l.z);
                light.type = LightType.Point;
                light.color = Hex(l.color);
                light.intensity = l.intensity;
                light.range = l.range;
                light.shadows = LightShadows.None; // cheap on the phone
            }
        }

        // ------------------------------------------------------------------ story: chapter 1
        static void BuildStory(Layout layout, Transform hud)
        {
            var holes = new List<Transform>();
            var dark = Mat("Apt_MouseHole", new Color(0.03f, 0.025f, 0.02f), 0f);
            foreach (var h in layout.mouse_holes ?? new Hole[0])
            {
                var hole = new GameObject(h.id).transform;
                hole.SetParent(level, false);
                // Forward points from the wall into the room.
                hole.SetPositionAndRotation(new Vector3(h.x, 0f, h.z), Quaternion.Euler(0f, FacingYaw(h.facing) + 180f, 0f));
                // A dark half disc on the wall: a flat cylinder, half under the floor.
                var arch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                arch.name = "Arch";
                Object.DestroyImmediate(arch.GetComponent<Collider>());
                arch.transform.SetParent(hole, false);
                arch.transform.localPosition = new Vector3(0f, 0f, 0.004f);
                arch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                arch.transform.localScale = new Vector3(0.09f, 0.003f, 0.11f);
                arch.GetComponent<Renderer>().sharedMaterial = dark;
                holes.Add(hole);
            }
            if (holes.Count == 0) return;

            BuildBirds(layout);
            Mouse mouse = MouseModel(holes);
            mouse.SetBounty("Kitchen mouse", 10);
            foreach (var t in layout.mouse_territories ?? new MouseTerritory[0]) BuildMouseTerritory(t);
            BuildFeathers(layout);
            BuildGardenCharacters();
            BuildNeighbourFlat();
            BuildCourtLife(layout);
            BuildGardenNight();
            BuildFox();
            BuildQuayLamp(layout.quay_lamp);
            BuildNightSky();
            BuildRain();
            new GameObject("WetWeather").AddComponent<WetWeather>().transform.SetParent(level, false);
            BuildHomecoming(layout);
            BuildGull(layout);
            BuildButterflies(layout);
            BuildFisherman(layout);
            BuildSunBeam(layout);
            BuildGlowWorms(layout.wild_garden);
            BuildCloudShadows();
            BuildGifts();
            BuildViewpoints();
            BuildShootingStars();
            BuildLightningBolt();
            BuildMorningMist(layout);
            BuildScratchSpots();
            BuildRainSplashes();
            var smoke = new GameObject("ChimneySmoke");
            smoke.transform.SetParent(level, false);
            smoke.AddComponent<ChimneySmoke>().Configure(AssetDatabase.LoadAssetAtPath<Material>($"{PlaygroundBuilder.MaterialDir}/Dust.mat"));
            new GameObject("DayCycle").AddComponent<DayCycle>().transform.SetParent(level, false);
            BuildCatnip(layout);
            BuildFarBankLamps(layout);
            BuildStreetLamps();
            BuildNapSpots();
            BountyBoard(layout.bounty_board);
            ProgressPanel(hud);
            new GameObject("Progress").AddComponent<Draho.Core.Progress>().transform.SetParent(level, false);
            new GameObject("PerfProbe").AddComponent<Draho.Core.PerfProbe>().transform.SetParent(level, false);
            var ring = new GameObject("NoiseRing", typeof(LineRenderer));
            ring.transform.SetParent(level, false);
            ring.GetComponent<LineRenderer>().sharedMaterial = NoiseRingMaterial();
            ring.AddComponent<NoiseRing>();
            var hudComp = StoryPanel(hud);
            var director = new GameObject("StoryDirector").AddComponent<StoryDirector>();
            director.Configure(hudComp, mouse, layout.rooms.Where(r => !r.outdoor).Min(r => r.x0));
            mouse.gameObject.SetActive(false); // the story lets it out
        }

        /// <summary>A small grey mouse made of primitives, with a CharacterController.</summary>
        static Mouse MouseModel(List<Transform> holes)
        {
            var root = new GameObject("Mouse");
            root.transform.SetParent(level, false);
            root.transform.SetPositionAndRotation(holes[0].position, holes[0].rotation);
            root.layer = 2; // the cat's layer; Mouse turns off cat-mouse collisions
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.035f;
            cc.height = 0.075f;
            cc.center = new Vector3(0f, 0.0375f, 0f);
            cc.stepOffset = 0.02f;
            cc.skinWidth = 0.005f;
            cc.minMoveDistance = 0f;

            var fur = Mat("Mouse_Fur", new Color(0.47f, 0.43f, 0.4f), 0.2f);
            var pink = Mat("Mouse_Pink", new Color(0.86f, 0.6f, 0.6f), 0.3f);
            var eye = Mat("Mouse_Eye", new Color(0.02f, 0.02f, 0.02f), 0.9f);
            var body = new GameObject("Body").transform;
            body.SetParent(root.transform, false);
            Transform Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.layer = 2;
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = scale;
                var r = go.GetComponent<Renderer>();
                r.sharedMaterial = m;
                r.shadowCastingMode = ShadowCastingMode.On;
                return go.transform;
            }
            Part(PrimitiveType.Capsule, "Torso", body, new Vector3(0f, 0.03f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.055f, 0.045f, 0.05f), fur);
            Part(PrimitiveType.Sphere, "Head", body, new Vector3(0f, 0.035f, 0.05f), Vector3.zero, new Vector3(0.042f, 0.038f, 0.05f), fur);
            Part(PrimitiveType.Sphere, "Nose", body, new Vector3(0f, 0.032f, 0.077f), Vector3.zero, Vector3.one * 0.009f, pink);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Sphere, "Ear", body, new Vector3(side * 0.017f, 0.058f, 0.042f), new Vector3(0f, side * 20f, 0f),
                    new Vector3(0.024f, 0.024f, 0.005f), pink);
                Part(PrimitiveType.Sphere, "Eye", body, new Vector3(side * 0.012f, 0.043f, 0.066f), Vector3.zero, Vector3.one * 0.008f, eye);
            }
            var tailPivot = new GameObject("TailPivot").transform;
            tailPivot.SetParent(body, false);
            tailPivot.localPosition = new Vector3(0f, 0.018f, -0.04f);
            Part(PrimitiveType.Cylinder, "Tail", tailPivot, new Vector3(0f, -0.006f, -0.05f), new Vector3(95f, 0f, 0f),
                new Vector3(0.006f, 0.05f, 0.006f), pink);

            var mouse = root.AddComponent<Mouse>();
            mouse.Configure(holes.ToArray(), body, tailPivot);
            return mouse;
        }

        /// <summary>Blackbirds on the lawn; they perch on the fence posts and on top of the walls.</summary>
        static void BuildBirds(Layout layout)
        {
            if (layout.birds == null || layout.birds.Length == 0) return;
            var perches = new List<Vector3>();
            foreach (var e in layout.garden_edges ?? new Edge[0])
            {
                bool alongX = Mathf.Approximately(e.from[1], e.to[1]);
                float line = alongX ? e.from[1] : e.from[0];
                float a = alongX ? Mathf.Min(e.from[0], e.to[0]) : Mathf.Min(e.from[1], e.to[1]);
                float b = alongX ? Mathf.Max(e.from[0], e.to[0]) : Mathf.Max(e.from[1], e.to[1]);
                float step = e.style == "posts" && e.post_spacing > 0f ? e.post_spacing : 3f;
                float top = e.style == "posts" ? e.h + 0.1f : e.h;
                for (float t = a + step; t < b - 0.5f; t += step)
                    perches.Add(alongX ? new Vector3(t, top, line) : new Vector3(line, top, t));
            }
            foreach (var bld in (layout.buildings ?? new Building[0]).Where(x => x.z1 >= -1f && x.x1 > -32f && x.roof != "pitched")) // roof edges by the garden
                for (float x = bld.x0 + 1.5f; x < bld.x1 - 1f; x += 3.5f)
                    perches.Add(new Vector3(x, bld.h + 0.06f, bld.z1 - 0.06f));
            foreach (var c in layout.climbables ?? new ClimbableInfo[0]) // the outer branches of trees
                if (c.type == "tree")
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = i * 2.1f + 0.4f;
                        perches.Add(new Vector3(c.x + Mathf.Cos(angle) * 1.1f, c.h + 0.6f, c.z + Mathf.Sin(angle) * 0.6f));
                    }
            var lawnZone = (layout.garden_zones ?? new Zone[0]).FirstOrDefault(z => z.material == "grass");
            var garden = layout.rooms.First(r => r.outdoor);
            var lawn = lawnZone != null
                ? new Vector4(lawnZone.x0 + 1.5f, garden.z0 + 0.8f, lawnZone.x1 - 0.5f, garden.z1 - 0.8f)
                : new Vector4(garden.x0 + 1.5f, garden.z0 + 0.8f, garden.x1 - 1.5f, garden.z1 - 0.8f);

            var black = Mat("Bird_Black", new Color(0.04f, 0.04f, 0.045f), 0.45f);
            var orange = Mat("Bird_Beak", new Color(0.95f, 0.6f, 0.08f), 0.5f);
            foreach (var spot in layout.birds)
            {
                var root = new GameObject(spot.id);
                root.transform.SetParent(level, false);
                root.transform.SetPositionAndRotation(new Vector3(spot.x, 0f, spot.z), Quaternion.Euler(0f, (spot.x * 37f) % 360f, 0f));
                Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 euler, Vector3 scale, Material m)
                {
                    var go = GameObject.CreatePrimitive(type);
                    Object.DestroyImmediate(go.GetComponent<Collider>());
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = pos;
                    go.transform.localRotation = Quaternion.Euler(euler);
                    go.transform.localScale = scale;
                    go.GetComponent<Renderer>().sharedMaterial = m;
                    if (type == PrimitiveType.Sphere) go.GetComponent<MeshFilter>().sharedMesh = LowSphere(); // small: few triangles
                    return go.transform;
                }
                Transform Pivot(string name, Transform parent, Vector3 pos)
                {
                    var t = new GameObject(name).transform;
                    t.SetParent(parent, false);
                    t.localPosition = pos;
                    return t;
                }
                // About 25 cm from beak to tail, like a real blackbird. +Z is forward.
                var body = Pivot("Body", root.transform, Vector3.zero);
                Part(PrimitiveType.Sphere, body, new Vector3(0f, 0.08f, 0f), new Vector3(-12f, 0f, 0f), new Vector3(0.085f, 0.08f, 0.15f), black);
                Part(PrimitiveType.Cube, body, new Vector3(0f, 0.1f, -0.11f), new Vector3(-20f, 0f, 0f), new Vector3(0.045f, 0.01f, 0.1f), black);
                foreach (float side in new[] { -1f, 1f })
                    Part(PrimitiveType.Cylinder, body, new Vector3(side * 0.015f, 0.022f, 0.005f), Vector3.zero, new Vector3(0.007f, 0.022f, 0.007f), orange);
                var head = Pivot("Head", body, new Vector3(0f, 0.11f, 0.05f));
                Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.02f, 0.02f), Vector3.zero, Vector3.one * 0.06f, black);
                Part(PrimitiveType.Cube, head, new Vector3(0f, 0.015f, 0.062f), new Vector3(8f, 0f, 0f), new Vector3(0.013f, 0.012f, 0.04f), orange);
                foreach (float side in new[] { -1f, 1f })
                    Part(PrimitiveType.Sphere, head, new Vector3(side * 0.024f, 0.028f, 0.035f), Vector3.zero, Vector3.one * 0.013f, orange); // eye ring
                Transform Wing(string name, float side)
                {
                    var pivot = Pivot(name, body, new Vector3(side * 0.04f, 0.105f, 0f));
                    Part(PrimitiveType.Cube, pivot, new Vector3(side * 0.006f, -0.03f, -0.01f), Vector3.zero, new Vector3(0.01f, 0.065f, 0.12f), black);
                    return pivot;
                }
                var left = Wing("WingL", -1f);
                var right = Wing("WingR", 1f);
                root.AddComponent<Bird>().Configure(body, head, left, right, perches.ToArray(), lawn);
            }
        }

        /// <summary>Fatsocat (the big white rival) and Zlatenka (the leopard tortoise) in the garden.</summary>
        static void BuildGardenCharacters()
        {
            var fat = PlaygroundBuilder.NpcCatModel("Fatsocat", level, new Color(0.93f, 0.93f, 0.9f), 1.45f);
            var model = fat.transform.GetChild(0);
            model.localScale = Vector3.Scale(model.localScale, new Vector3(1.3f, 1.05f, 1.1f)); // very fat
            fat.transform.SetPositionAndRotation(new Vector3(-23.6f, 0f, 2.5f), Quaternion.Euler(0f, 90f, 0f));
            fat.AddComponent<Fatsocat>().Configure(fat.GetComponentInChildren<Animator>());

            var tortoise = new GameObject("Zlatenka");
            tortoise.transform.SetParent(level, false);
            tortoise.transform.SetPositionAndRotation(new Vector3(-12f, 0f, 3.2f), Quaternion.Euler(0f, 250f, 0f));
            var shellMat = Mat("Tortoise_Shell", new Color(0.78f, 0.66f, 0.42f), 0.35f);
            var spotMat = Mat("Tortoise_Spots", new Color(0.18f, 0.13f, 0.08f), 0.3f);
            var skin = Mat("Tortoise_Skin", new Color(0.62f, 0.56f, 0.44f), 0.2f);
            Transform Part(Transform parent, Vector3 pos, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go.transform;
            }
            Part(tortoise.transform, new Vector3(0f, 0.09f, 0f), new Vector3(0.3f, 0.2f, 0.36f), shellMat);
            var rng = new System.Random(3);
            for (int i = 0; i < 18; i++) // leopard spots on the shell
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, up = 0.3f + 0.6f * (float)rng.NextDouble();
                var n = new Vector3(Mathf.Cos(a) * Mathf.Sqrt(1f - up * up), up, Mathf.Sin(a) * Mathf.Sqrt(1f - up * up));
                Part(tortoise.transform, new Vector3(0f, 0.09f, 0f) + Vector3.Scale(n, new Vector3(0.15f, 0.1f, 0.18f)), Vector3.one * 0.035f, spotMat);
            }
            var head = new GameObject("Head").transform;
            head.SetParent(tortoise.transform, false);
            head.localPosition = new Vector3(0f, 0.07f, 0.19f);
            Part(head, new Vector3(0f, 0f, 0.04f), new Vector3(0.07f, 0.06f, 0.09f), skin);
            foreach (var leg in new[] { new Vector3(-0.11f, 0.03f, 0.1f), new Vector3(0.11f, 0.03f, 0.1f), new Vector3(-0.11f, 0.03f, -0.1f), new Vector3(0.11f, 0.03f, -0.1f) })
                Part(tortoise.transform, leg, new Vector3(0.06f, 0.06f, 0.06f), skin);
            tortoise.AddComponent<Tortoise>().Configure(head, new[]
            {
                new Vector3(-12f, 0f, 3.2f), new Vector3(-16f, 0f, 2.2f), new Vector3(-9f, 0f, 1.6f), new Vector3(-14f, 0f, 4.1f),
            });
        }

        /// <summary>
        /// The neighbour's flat behind the apartment (chapter 7), reached through an open window from the wild
        /// garden: plaster walls (open at the top, like the apartment), a wooden floor, a sofa, a rug, a
        /// shelf, a food bowl, Hrozna's basket with Drahomil's toy mouse, and Hrozna herself.
        /// </summary>
        static void BuildNeighbourFlat()
        {
            const float x0 = -2f, x1 = 7.6f, z0 = 5.25f, z1 = 9.0f, h = 2.6f, t = 0.15f;
            var root = new GameObject("NeighbourFlat").transform;
            root.SetParent(level, false);
            var plaster = Mat("Flat_Wall", new Color(0.86f, 0.82f, 0.74f), 0.15f);
            var floorMat = Mat("Flat_Floor", new Color(0.55f, 0.4f, 0.27f), 0.35f, TileTexture(), new Vector2(x1 - x0, z1 - z0) / 0.25f);
            Box("FlatFloor", new Vector3((x0 + x1) / 2, -0.04f, (z0 + z1) / 2), new Vector3(x1 - x0, 0.1f, z1 - z0), floorMat).transform.SetParent(root, true);
            // West wall with an open window (sill 0.55 m) towards the wild garden.
            const float w0 = 6.6f, w1 = 7.6f, sill = 0.55f, top = 1.45f;
            WallSegment("FlatWall_W", false, x0 - t / 2, z0, w0, 0f, h, t, plaster);
            WallSegment("FlatWall_W", false, x0 - t / 2, w1, z1, 0f, h, t, plaster);
            WallSegment("FlatWall_W_sill", false, x0 - t / 2, w0, w1, 0f, sill, t, plaster);
            WallSegment("FlatWall_W_head", false, x0 - t / 2, w0, w1, top, h, t, plaster);
            WallSegment("FlatWall_N", true, z1 + t / 2, x0 - t, x1 + t, 0f, h, t, plaster);
            WallSegment("FlatWall_E", false, x1 + t / 2, z0, z1, 0f, h, t, plaster);
            WallSegment("FlatWall_S", true, z0 - t / 2, x0 - t, 0f, 0f, h, t, plaster); // the apartment wall covers x 0..7.6
            var frameMat = Mat("Flat_WindowFrame", new Color(0.28f, 0.2f, 0.13f), 0.3f);
            Box("WindowSill", new Vector3(x0 - t / 2, sill + 0.015f, (w0 + w1) / 2), new Vector3(t + 0.12f, 0.03f, w1 - w0 + 0.1f),
                Mat("Flat_Sill", new Color(0.88f, 0.86f, 0.8f), 0.3f)).transform.SetParent(root, true);
            foreach (var z in new[] { w0, w1 })
                Box("WindowFrame", new Vector3(x0 - t / 2, (sill + top) / 2, z), new Vector3(t + 0.04f, top - sill, 0.06f), frameMat, collider: false).transform.SetParent(root, true);
            Box("WindowFrame", new Vector3(x0 - t / 2, top, (w0 + w1) / 2), new Vector3(t + 0.04f, 0.06f, w1 - w0 + 0.06f), frameMat, collider: false).transform.SetParent(root, true);
            foreach (var shutter in new[] { w0 - 0.3f, w1 + 0.3f }) // open green shutters
                Box("Shutter", new Vector3(x0 - 0.2f, (sill + top) / 2, shutter), new Vector3(0.04f, top - sill, 0.5f),
                    Mat("Flat_Shutter", new Color(0.2f, 0.36f, 0.26f), 0.3f), collider: false).transform.SetParent(root, true);

            var fabric = Mat("Flat_Sofa", new Color(0.36f, 0.42f, 0.55f), 0.2f);
            Box("Sofa", new Vector3(2.5f, 0.22f, 8.55f), new Vector3(2.0f, 0.44f, 0.8f), fabric).transform.SetParent(root, true);
            Box("SofaBack", new Vector3(2.5f, 0.6f, 8.85f), new Vector3(2.0f, 0.5f, 0.2f), fabric).transform.SetParent(root, true);
            Box("Rug", new Vector3(2.5f, 0.012f, 7.2f), new Vector3(2.4f, 0.02f, 1.6f), Mat("Flat_Rug", new Color(0.62f, 0.3f, 0.22f), 0.1f), collider: false).transform.SetParent(root, true);
            Box("Shelf", new Vector3(5.2f, 0.9f, 8.8f), new Vector3(1.4f, 1.8f, 0.35f), Mat("Flat_Shelf", new Color(0.45f, 0.32f, 0.2f), 0.2f)).transform.SetParent(root, true);
            var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bowl.name = "DogBowl";
            Object.DestroyImmediate(bowl.GetComponent<Collider>());
            bowl.transform.SetParent(root, false);
            bowl.transform.position = new Vector3(0.6f, 0.04f, 5.7f);
            bowl.transform.localScale = new Vector3(0.26f, 0.04f, 0.26f);
            bowl.GetComponent<Renderer>().sharedMaterial = Mat("Flat_Bowl", new Color(0.75f, 0.18f, 0.16f), 0.6f);

            // Hrozna's basket in the far corner, with the toy mouse on the cushion.
            var basketSpot = new GameObject("HroznaBasket").transform;
            basketSpot.SetParent(root, false);
            basketSpot.position = new Vector3(6.8f, 0f, 6.2f);
            var wicker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(wicker.GetComponent<Collider>());
            wicker.transform.SetParent(root, false);
            wicker.transform.position = basketSpot.position + Vector3.up * 0.06f;
            wicker.transform.localScale = new Vector3(0.95f, 0.06f, 0.8f);
            wicker.GetComponent<Renderer>().sharedMaterial = Mat("Flat_Wicker", new Color(0.66f, 0.5f, 0.3f), 0.2f);
            var cushion = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cushion.GetComponent<Collider>());
            cushion.transform.SetParent(root, false);
            cushion.transform.position = basketSpot.position + Vector3.up * 0.1f;
            cushion.transform.localScale = new Vector3(0.8f, 0.03f, 0.65f);
            cushion.GetComponent<Renderer>().sharedMaterial = Mat("Flat_Cushion", new Color(0.82f, 0.74f, 0.6f), 0.1f);
            var toy = new GameObject("ToyMouse");
            toy.transform.SetParent(level, false);
            toy.transform.position = basketSpot.position + new Vector3(-0.45f, 0.06f, -0.3f); // dropped by the basket, close to Hrozna's nose
            var toyBody = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.DestroyImmediate(toyBody.GetComponent<Collider>());
            toyBody.transform.SetParent(toy.transform, false);
            toyBody.transform.localRotation = Quaternion.Euler(90f, 30f, 0f);
            toyBody.transform.localScale = new Vector3(0.05f, 0.04f, 0.05f);
            toyBody.GetComponent<Renderer>().sharedMaterial = Mat("Toy_Felt", new Color(0.55f, 0.55f, 0.6f), 0.1f);
            toy.AddComponent<StoryItem>().Configure("Drahomil's toy mouse! Now get out before Hrozna wakes up.");

            // Hrozna: a red Cardigan corgi with a white chest and her long fox tail.
            var dog = new GameObject("Hrozna");
            dog.transform.SetParent(level, false);
            dog.transform.SetPositionAndRotation(basketSpot.position, Quaternion.Euler(0f, 250f, 0f));
            var red = Mat("Corgi_Red", new Color(0.72f, 0.42f, 0.2f), 0.25f);
            var white = Mat("Corgi_White", new Color(0.94f, 0.92f, 0.88f), 0.25f);
            var dark = Mat("Mouse_Eye", new Color(0.02f, 0.02f, 0.02f), 0.9f);
            var body = new GameObject("Body").transform;
            body.SetParent(dog.transform, false);
            Transform Part(Transform parent, Vector3 pos, Vector3 scale, Material m, PrimitiveType type = PrimitiveType.Sphere)
            {
                var go = GameObject.CreatePrimitive(type);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go.transform;
            }
            Part(body, new Vector3(0f, 0.2f, 0f), new Vector3(0.26f, 0.22f, 0.6f), red);
            Part(body, new Vector3(0f, 0.17f, 0.2f), new Vector3(0.2f, 0.18f, 0.2f), white);         // chest
            Part(body, new Vector3(0f, 0.33f, 0.33f), new Vector3(0.2f, 0.19f, 0.22f), red);         // head
            Part(body, new Vector3(0f, 0.29f, 0.45f), new Vector3(0.1f, 0.09f, 0.12f), white);       // muzzle
            Part(body, new Vector3(0f, 0.3f, 0.51f), Vector3.one * 0.035f, dark);                     // nose
            foreach (float sx in new[] { -1f, 1f })
            {
                var ear = Part(body, new Vector3(sx * 0.075f, 0.44f, 0.31f), new Vector3(0.09f, 0.11f, 0.035f), red); // big, but corgi-sized
                ear.localRotation = Quaternion.Euler(0f, 0f, sx * -22f);
                Part(body, new Vector3(sx * 0.055f, 0.36f, 0.43f), Vector3.one * 0.03f, dark);       // eyes
                foreach (float fz in new[] { 0.18f, -0.18f })
                    Part(body, new Vector3(sx * 0.08f, 0.06f, fz), new Vector3(0.07f, 0.12f, 0.07f), white, PrimitiveType.Cylinder);
            }
            var tail = new GameObject("Tail").transform;
            tail.SetParent(body, false);
            tail.localPosition = new Vector3(0f, 0.22f, -0.3f);
            var tailPart = Part(tail, new Vector3(0f, -0.02f, -0.16f), new Vector3(0.09f, 0.09f, 0.34f), red);
            tailPart.localRotation = Quaternion.Euler(20f, 0f, 0f);
            dog.transform.localScale = Vector3.one * 0.85f;
            dog.AddComponent<AudioSource>().playOnAwake = false;
            dog.AddComponent<Hrozna>().Configure(body, tail, basketSpot);

            // Hrozna's rubber toys on the floor between the window and her basket: step on one and it squeaks.
            var toyMats = new[]
            {
                Mat("Rubber_Yellow", new Color(0.98f, 0.82f, 0.15f), 0.6f),
                Mat("Rubber_Red", new Color(0.88f, 0.18f, 0.15f), 0.6f),
                Mat("Rubber_Blue", new Color(0.2f, 0.45f, 0.9f), 0.6f),
            };
            var spots = new[] { new Vector3(0.3f, 0f, 6.6f), new Vector3(1.1f, 0f, 7.7f), new Vector3(2.9f, 0f, 6.1f),
                                new Vector3(4.2f, 0f, 7.2f), new Vector3(5.3f, 0f, 6.0f), new Vector3(5.6f, 0f, 7.6f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var squeaker = new GameObject($"SqueakyToy_{i + 1}");
                squeaker.transform.SetParent(level, false);
                squeaker.transform.position = spots[i];
                var m = toyMats[i % toyMats.Length];
                if (i % 3 == 0) // a rubber duck
                {
                    Part(squeaker.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.08f, 0.13f), m);
                    Part(squeaker.transform, new Vector3(0f, 0.11f, 0.04f), Vector3.one * 0.06f, m);
                    Part(squeaker.transform, new Vector3(0f, 0.11f, 0.08f), new Vector3(0.03f, 0.015f, 0.03f), toyMats[1]);
                }
                else if (i % 3 == 1) // a bone
                {
                    Part(squeaker.transform, new Vector3(0f, 0.025f, 0f), new Vector3(0.04f, 0.04f, 0.16f), m, PrimitiveType.Capsule)
                        .localRotation = Quaternion.Euler(90f, 0f, 0f);
                    foreach (float end in new[] { -1f, 1f })
                        Part(squeaker.transform, new Vector3(0f, 0.025f, end * 0.08f), new Vector3(0.06f, 0.045f, 0.04f), m);
                }
                else // a ball
                    Part(squeaker.transform, new Vector3(0f, 0.045f, 0f), Vector3.one * 0.09f, m);
                squeaker.transform.rotation = Quaternion.Euler(0f, i * 67f, 0f);
                squeaker.AddComponent<AudioSource>().playOnAwake = false;
                squeaker.AddComponent<LineRenderer>().sharedMaterial = NoiseRingMaterial();
                squeaker.AddComponent<SqueakyToy>();
            }
        }

        /// <summary>A cork board on the kitchen wall with paper notes: the mouse bounties.</summary>
        static void BountyBoard(float[] at)
        {
            if (at == null || at.Length < 4) return;
            var root = new GameObject("BountyBoard").transform;
            root.SetParent(level, false);
            root.SetPositionAndRotation(new Vector3(at[0], at[1], at[2]), Quaternion.Euler(0f, at[3], 0f));
            var cork = Mat("Apt_Cork", new Color(0.66f, 0.5f, 0.33f), 0.1f);
            var frame = Mat("Apt_BoardFrame", new Color(0.35f, 0.25f, 0.16f), 0.2f);
            var paper = Mat("Apt_Paper", new Color(0.95f, 0.93f, 0.86f), 0.1f);
            var pin = Mat("Apt_Pin", new Color(0.8f, 0.15f, 0.12f), 0.6f);
            Part(root, "Frame", new Vector3(0f, 0f, 0f), new Vector3(0.66f, 0.48f, 0.03f), frame);
            Part(root, "Cork", new Vector3(0f, 0f, 0.01f), new Vector3(0.6f, 0.42f, 0.02f), cork);
            var notes = new[] { new Vector3(-0.17f, 0.08f, 0f), new Vector3(0.05f, 0.1f, 0f), new Vector3(0.18f, -0.06f, 0f), new Vector3(-0.08f, -0.1f, 0f) };
            for (int i = 0; i < notes.Length; i++)
            {
                var note = Part(root, "Note", notes[i] + new Vector3(0f, 0f, 0.022f), new Vector3(0.12f, 0.15f, 0.004f), paper);
                note.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 1f : -1f) * 6f);
                Part(root, "Pin", notes[i] + new Vector3(0f, 0.06f, 0.028f), Vector3.one * 0.015f, pin);
            }
            var marker = new GameObject("Marker_BountyBoard").transform;
            marker.SetParent(level, false);
            marker.position = new Vector3(at[0], 0f, at[2]) + root.forward * 0.4f;

            Transform Part(Transform parent, string name, Vector3 local, Vector3 scale, Material m)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = local;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = m;
                return go.transform;
            }
        }

        static Material NoiseRingMaterial()
        {
            // Sprites/Default: unlit, alpha-blended and uses the line's vertex colours.
            string path = $"{PlaygroundBuilder.MaterialDir}/NoiseRing.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        /// <summary>Coins and feathers (top right) and the bounty list panel (shown at the board).</summary>
        static void ProgressPanel(Transform canvas)
        {
            Text MakeText(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                var rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = anchor;
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
                var t = go.GetComponent<Text>();
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = fontSize;
                t.alignment = align;
                t.color = new Color(1f, 0.92f, 0.7f);
                t.raycastTarget = false;
                go.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
                return t;
            }
            var counters = MakeText("Counters", canvas, new Vector2(1f, 1f), new Vector2(-40f, -230f), new Vector2(700f, 60f), 36, TextAnchor.MiddleRight);
            counters.horizontalOverflow = HorizontalWrapMode.Overflow; // right-aligned: a longer line grows to the left
            var panel = new GameObject("BountyPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var prt = (RectTransform)panel.transform;
            prt.SetParent(canvas, false);
            prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 40f);
            prt.sizeDelta = new Vector2(1100f, 420f);
            var bg = panel.GetComponent<Image>();
            bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.25f, 0.17f, 0.1f, 0.85f);
            bg.raycastTarget = false;
            var group = panel.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var board = MakeText("BountyList", prt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1020f, 380f), 34, TextAnchor.MiddleCenter);
            var hud = canvas.gameObject.AddComponent<ProgressHud>();
            hud.Configure(counters, board, group);

            // Goal compass under the story panel: a chevron of two bars and the distance.
            var compass = new GameObject("GoalCompass", typeof(RectTransform), typeof(CanvasGroup));
            var crt = (RectTransform)compass.transform;
            crt.SetParent(canvas, false);
            crt.anchorMin = crt.anchorMax = crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -232f);
            crt.sizeDelta = new Vector2(240f, 110f);
            var cg = compass.GetComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            var arrow = new GameObject("Arrow", typeof(RectTransform)).GetComponent<RectTransform>();
            arrow.SetParent(crt, false);
            arrow.anchorMin = arrow.anchorMax = new Vector2(0.5f, 1f);
            arrow.anchoredPosition = new Vector2(0f, -30f);
            arrow.sizeDelta = new Vector2(50f, 50f);
            foreach (float side in new[] { -1f, 1f })
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                bar.SetParent(arrow, false);
                bar.sizeDelta = new Vector2(10f, 34f);
                bar.anchoredPosition = new Vector2(side * 9f, 0f);
                bar.localRotation = Quaternion.Euler(0f, 0f, side * 40f);
                var img = bar.GetComponent<Image>();
                img.color = new Color(1f, 0.85f, 0.45f, 0.95f);
                img.raycastTarget = false;
                bar.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f);
            }
            var dist = MakeText("Distance", crt, new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(240f, 44f), 30, TextAnchor.MiddleCenter);
            compass.AddComponent<GoalCompass>().Configure(arrow, dist, cg);
        }

        static StoryHud StoryPanel(Transform canvas)
        {
            var panel = new GameObject("StoryPanel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)panel.transform;
            rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1500f, 200f);
            rt.anchoredPosition = new Vector2(0f, -24f);
            var bg = panel.GetComponent<Image>();
            bg.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.05f, 0.05f, 0.07f, 0.55f);
            bg.raycastTarget = false;
            var group = panel.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            Text Line(string name, float top, float height, int size, FontStyle style, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Text));
                var t = go.GetComponent<Text>();
                var trt = (RectTransform)go.transform;
                trt.SetParent(rt, false);
                trt.anchorMin = new Vector2(0f, 1f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.pivot = new Vector2(0.5f, 1f);
                trt.offsetMin = new Vector2(36f, -top - height);
                trt.offsetMax = new Vector2(-36f, -top);
                t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                t.fontSize = size;
                t.fontStyle = style;
                t.color = color;
                t.alignment = TextAnchor.MiddleCenter;
                t.horizontalOverflow = HorizontalWrapMode.Wrap;
                // Long lines shrink to fit their box, so the story never runs into the goal.
                t.verticalOverflow = VerticalWrapMode.Truncate;
                t.resizeTextForBestFit = true;
                t.resizeTextMinSize = Mathf.RoundToInt(size * 0.62f);
                t.resizeTextMaxSize = size;
                t.raycastTarget = false;
                return t;
            }
            Text story = Line("Story", 12f, 104f, 48, FontStyle.Italic, new Color(1f, 1f, 1f, 0.95f));
            Text goal = Line("Goal", 120f, 70f, 42, FontStyle.Bold, new Color(1f, 0.82f, 0.4f));

            var hud = panel.AddComponent<StoryHud>();
            var so = new SerializedObject(hud);
            so.FindProperty("story").objectReferenceValue = story;
            so.FindProperty("goal").objectReferenceValue = goal;
            so.FindProperty("group").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        // ------------------------------------------------------------------ helpers
        /// <summary>Kenney models face -Z. Yaw that turns -Z towards the given compass direction.</summary>
        static float FacingYaw(string facing)
        {
            switch (facing)
            {
                case "north": return 180f;
                case "east": return -90f;
                case "west": return 90f;
                default: return 0f;
            }
        }

        static GameObject Box(string name, Vector3 center, Vector3 size, Material mat, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(level, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

        static Material Mat(string name, Color color, float smoothness, Texture tex = null, Vector2? tiling = null)
        {
            if (materials.TryGetValue(name, out var m)) return m;
            m = PlaygroundBuilder.LitMaterial(name, color, smoothness, tex, tiling);
            materials[name] = m;
            return m;
        }

        static Material GlassMat()
        {
            if (materials.TryGetValue("Apt_Glass", out var m)) return m;
            m = PlaygroundBuilder.LitMaterial("Apt_Glass", new Color(0.75f, 0.88f, 0.95f, 0.18f), 0.95f);
            // URP Lit, transparent surface.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            materials["Apt_Glass"] = m;
            return m;
        }

        /// <summary>
        /// Roof slates or tiles, 1 m square (white, tinted by the material): 4 rows of 5, every second row shifted,
        /// a dark shadow line under each row (the overlap) and a little variation per slate. vertical: rows along v.
        /// </summary>
        static Texture2D RoofTexture(bool vertical) => MakeTexture(vertical ? "RoofSlatesV.png" : "RoofSlates.png", 256, (px, py, size) =>
        {
            int x = vertical ? py : px, y = vertical ? px : py;
            const int rowH = 64, slateW = 51;
            int row = y / rowH;
            int shifted = x + (row % 2) * slateW / 2;
            int col = shifted / slateW;
            int inRow = y % rowH;
            if (shifted % slateW < 2) return new Color(0.5f, 0.5f, 0.52f);  // the gap between two slates
            int hash = (row * 73856093) ^ (col * 19349663);
            float tone = 0.8f + 0.2f * (((hash >> 4) & 15) / 15f);
            float shade = inRow < 6 ? 0.55f + inRow * 0.06f : 1f;           // shadow of the row above
            float edge = inRow > rowH - 4 ? 1.08f : 1f;                     // a light lower edge
            float n = Mathf.PerlinNoise(px * 0.05f, py * 0.05f) * 0.08f;
            float t = Mathf.Clamp01(tone * shade * edge - n);
            return new Color(t, t, t);
        });

        static Texture2D TileTexture() => MakeTexture("FloorTile.png", 128, (x, y, size) =>
        {
            bool grout = x < 2 || y < 2;
            float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.06f;
            return grout ? new Color(0.62f, 0.6f, 0.57f) : new Color(0.93f + n, 0.92f + n, 0.9f + n);
        });

        /// <summary>
        /// Old plaster, 3 m square (white, tinted by the material): soft unevenness, a few faint damp stains and fine grit.
        /// </summary>
        static Texture2D PlasterTexture() => MakeTexture("Plaster.png", 256, (x, y, size) =>
        {
            float broad = TileNoise(x, y, size, 0.012f, 3.1f, 7.7f);
            float stain = Mathf.Clamp01((TileNoise(x, y, size, 0.03f, 11f, 5f) - 0.62f) * 3f);
            int h = (x * 73856093) ^ (y * 19349663);
            float grit = ((h >> 5) & 15) / 15f * 0.05f;
            float t = 0.9f + broad * 0.1f - stain * 0.12f - grit;
            return new Color(t, t * 0.985f, t * 0.96f);
        });

        /// <summary>Perlin noise that wraps round at the texture edges (no seams when the texture repeats).</summary>
        static float TileNoise(int x, int y, int size, float scale, float ox, float oy)
        {
            float fx = (float)x / size, fy = (float)y / size;
            float N(float px, float py) => Mathf.PerlinNoise(px * scale + ox, py * scale + oy);
            float top = Mathf.Lerp(N(x, y), N(x - size, y), fx);
            float bottom = Mathf.Lerp(N(x, y - size), N(x - size, y - size), fx);
            return Mathf.Lerp(top, bottom, fy);
        }

        static Texture2D PebbleTexture() => MakeTexture("Pebbles.png", 256, (x, y, size) =>
        {
            // Round stones: a jittered grid of discs, light on top, dark gaps between them.
            const int cell = 32;
            float best = 9f;
            float shade = 0f;
            int cx = x / cell, cy = y / cell;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int gx = cx + dx, gy = cy + dy;
                int hash = ((gx & 7) * 73856093) ^ ((gy & 7) * 19349663);
                float jx = ((hash >> 3) & 15) / 15f, jy = ((hash >> 7) & 15) / 15f;
                float px = (gx + 0.25f + 0.5f * jx) * cell, py = (gy + 0.25f + 0.5f * jy) * cell;
                float r = cell * (0.42f + 0.12f * (((hash >> 11) & 7) / 7f));
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(Repeat(px, x, size), Repeat(py, y, size))) / r;
                if (dist < best) { best = dist; shade = 0.85f + 0.15f * (((hash >> 5) & 7) / 7f); }
            }
            if (best > 1f) return new Color(0.35f, 0.33f, 0.3f);
            float light = Mathf.Lerp(1.05f, 0.75f, best * best) * shade;
            return new Color(light, light * 0.97f, light * 0.92f);
        });

        static Texture2D RippleTexture() => MakeTexture("Ripples.png", 128, (x, y, size) =>
        {
            // Soft long ripples across the flow, darker troughs; tiles seamlessly.
            float u = x * 2f * Mathf.PI / size, v = y * 2f * Mathf.PI / size;
            float r = 0.5f + 0.25f * Mathf.Sin(v * 3f + Mathf.Sin(u * 2f) * 1.5f) + 0.15f * Mathf.Sin(u * 5f + v * 7f) + 0.1f * Mathf.Sin(v * 11f - u * 3f);
            float t = 0.75f + 0.35f * r;
            return new Color(t, t, t);
        });

        static Texture2D GrassTexture() => MakeTexture("Grass.png", 256, (x, y, size) =>
        {
            // Short blades (fine noise) over soft patches (smooth noise that tiles), low contrast.
            int h1 = (x * 73856093) ^ (y * 19349663);
            float blades = ((h1 >> 4) & 255) / 255f;
            float u = x * 2f * Mathf.PI / size, w = y * 2f * Mathf.PI / size;
            float patches = 0.5f + 0.25f * Mathf.Sin(u * 3f + Mathf.Sin(w * 2f)) * Mathf.Cos(w * 4f + 0.7f * Mathf.Sin(u * 5f))
                            + 0.25f * Mathf.Sin(u * 7f + w * 5f) * 0.5f;
            float v = 0.85f + 0.12f * blades + 0.15f * (patches - 0.5f);
            float dry = Mathf.Clamp01((patches - 0.65f) * 3f) * 0.12f; // some yellowish patches
            return new Color((0.62f + dry) * v, 0.72f * v, 0.5f * v);
        });

        static float Repeat(float p, int pixel, int size)
        {
            // Choose the copy of p (wrapped around the texture) nearest to the pixel, so the texture tiles.
            float best = p;
            foreach (float c in new[] { p - size, p + size })
                if (Mathf.Abs(c - pixel) < Mathf.Abs(best - pixel)) best = c;
            return best;
        }

        static Texture2D MakeTexture(string file, int size, Func<int, int, int, Color> pixel)
        {
            string path = $"{PlaygroundBuilder.ArtDir}/Textures/{file}";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, pixel(x, y, size));
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
