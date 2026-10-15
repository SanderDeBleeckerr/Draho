using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
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
        }
        [Serializable] class FloorInfo { public float tile_size; public string color; }
        [Serializable] class WallInfo { public float height; public float thickness; public string color; }
        [Serializable] class Room { public string id; public float x0, z0, x1, z1; public bool outdoor; }
        [Serializable] class Opening { public string id; public string wall; public float from, to, height; }
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
        [Serializable] class Edge { public string id, color; public float[] from, to; public float h; }
        [Serializable] class Spawn { public float x, z; public string facing; }
#pragma warning restore 0649

        // ------------------------------------------------------------------ build
        public static void Build(GameObject catPrefab)
        {
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(LayoutPath));
            materials.Clear();

            Scene scene = PlaygroundBuilder.NewLitScene(Quaternion.Euler(55f, 150f, 0f));
            level = new GameObject("Apartment").transform;

            Rect inside = InsideRect(layout);
            BuildFloors(layout, inside);
            BuildOuterWalls(layout, inside);
            foreach (var court in layout.courts ?? new Court[0]) BuildCourt(layout, court);
            foreach (var prop in layout.props) BuildProp(prop);
            BuildGarden(layout);

            var spawn = new GameObject("SpawnPoint").transform;
            spawn.SetParent(level, false);
            spawn.SetPositionAndRotation(new Vector3(layout.spawn.x, 0.02f, layout.spawn.z),
                Quaternion.Euler(0f, FacingYaw(layout.spawn.facing) + 180f, 0f)); // the cat looks along +Z
            PlaygroundBuilder.AddPlayerRig(scene, catPrefab, spawn, new Color(0.62f, 0.72f, 0.85f));

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
            var terrace = Mat("Apt_Terrace", new Color(0.55f, 0.53f, 0.5f), 0.2f, TileTexture(),
                new Vector2(garden.x1 - garden.x0, garden.z1 - garden.z0) / 0.5f);
            Box("Terrace", new Vector3((garden.x0 + garden.x1) / 2, -0.06f, (garden.z0 + garden.z1) / 2),
                new Vector3(garden.x1 - garden.x0, 0.1f, garden.z1 - garden.z0), terrace);
            var edges = (layout.garden_edges ?? new Edge[0]).ToList();
            // Close the south side too, so the garden is a closed space.
            edges.Add(new Edge { id = "fence_south", color = "#8A7A66", from = new[] { garden.x0, garden.z0 }, to = new[] { garden.x1, garden.z0 }, h = 1.8f });
            foreach (var e in edges)
            {
                bool alongX = Mathf.Approximately(e.from[1], e.to[1]);
                float line = alongX ? e.from[1] : e.from[0];
                float a = alongX ? Mathf.Min(e.from[0], e.to[0]) : Mathf.Min(e.from[1], e.to[1]);
                float b = alongX ? Mathf.Max(e.from[0], e.to[0]) : Mathf.Max(e.from[1], e.to[1]);
                WallSegment(e.id, alongX, line, a, b, 0f, e.h, 0.08f, Mat($"Apt_{e.id}", Hex(e.color), 0.1f));
            }
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
