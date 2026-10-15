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
        }
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
            public float x0, z0, x1, z1, glass_height;
            public string[] glass, outer_walls;
            public Door[] doors;
        }
        [Serializable] class Door { public string id, side; public float from, to, open_gap; }
        [Serializable] class Edge
        {
            public string id, color, style, post_color, material;
            public float[] from, to;
            public float h, post_spacing;
        }
        [Serializable] class Spawn { public float x, z; public string facing; }
        [Serializable] class Hole { public string id, facing; public float x, z; }
        [Serializable] class ClimbableInfo
        {
            public string id, type, color, top_color;
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

            Scene scene = PlaygroundBuilder.NewLitScene(Quaternion.Euler(45f, 240f, 0f)); // real afternoon sun (see garden_note)
            level = new GameObject("Apartment").transform;

            Rect inside = InsideRect(layout);
            BuildFloors(layout, inside);
            BuildOuterWalls(layout, inside);
            foreach (var court in layout.courts ?? new Court[0]) BuildCourt(layout, court);
            foreach (var prop in layout.props) BuildProp(prop);
            BuildGarden(layout);
            BuildPaths(layout);
            foreach (var c in layout.climbables ?? new ClimbableInfo[0]) BuildClimbable(c);

            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(level, false);
            spawn.SetPositionAndRotation(new Vector3(layout.spawn.x, 0.02f, layout.spawn.z),
                Quaternion.Euler(0f, FacingYaw(layout.spawn.facing) + 180f, 0f)); // the cat looks along +Z
            Transform hud = PlaygroundBuilder.AddPlayerRig(scene, catPrefab, spawn, new Color(0.62f, 0.72f, 0.85f));
            BuildLamps(layout);
            BuildStory(layout, hud);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"Draho: built '{layout.name}' with {layout.props.Length} props -> {ScenePath}");
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
            var pebbles = Mat("Apt_Pebbles", Hex(c.color), 0.2f, PebbleTexture(), new Vector2(c.x1 - c.x0, c.z1 - c.z0) / 0.5f);
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
            for (int r = 0; r < rows; r++)
                Box("Plank", At((a + b) / 2, 0.04f + r * (board + gap) + board / 2), Size(b - a, board, 0.03f),
                    r % 2 == 0 ? plank : plankDark, collider: false).transform.SetParent(root, true);
            Box("Collider", At((a + b) / 2, e.h / 2), Size(b - a, e.h, 0.12f), null).transform.SetParent(root, true);
            Object.DestroyImmediate(root.Find("Collider").GetComponent<Renderer>());
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
                    uvs.Add(new Vector2(colour, 0.5f));
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

        static Material IvyLeafMat()
        {
            if (materials.TryGetValue("Apt_IvyLeaves", out var m)) return m;
            m = PlaygroundBuilder.LitMaterial("Apt_IvyLeaves", Color.white, 0.35f, IvyColours());
            m.SetFloat("_Cull", 0f); // both faces: thin leaves
            m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
            materials["Apt_IvyLeaves"] = m;
            return m;
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
                t.verticalOverflow = VerticalWrapMode.Overflow;
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

        static Texture2D TileTexture() => MakeTexture("FloorTile.png", 128, (x, y, size) =>
        {
            bool grout = x < 2 || y < 2;
            float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.06f;
            return grout ? new Color(0.62f, 0.6f, 0.57f) : new Color(0.93f + n, 0.92f + n, 0.9f + n);
        });

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
