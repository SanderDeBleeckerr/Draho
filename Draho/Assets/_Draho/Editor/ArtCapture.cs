using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Draho.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Draho.EditorTools
{
    /// <summary>
    /// Art review screenshots (docs/ART_STYLE.md, phase 3): builds the scenes, opens the apartment and renders
    /// fixed camera views in several SkyMood moods, so "before" and "after" pictures come from the same places.
    ///
    ///   Unity -batchmode -projectPath Draho -executeMethod Draho.EditorTools.ArtCapture.Run
    ///         -captureOut D:/captures/after [-captureMoods Day,Evening,Night] -quit
    ///
    /// Run CiBuild.Prepare once before (it saves project settings that need an editor restart).
    /// In edit mode the cat stands in its stored pose, and scripts that react to the night in Update
    /// (lamps, window glow) keep their day state: the pictures judge colour, light and readability.
    /// </summary>
    public static class ArtCapture
    {
        // name, camera position, look-at point (Unity metres; living x 0..4.6, kitchen x 4.6..7.6, z 0..5)
        static readonly (string name, Vector3 pos, Vector3 target)[] FixedViews =
        {
            ("living", new Vector3(4.35f, 1.6f, 0.35f), new Vector3(1.4f, 0.3f, 3.8f)),        // the living room, high
            ("living_low", new Vector3(4.3f, 0.35f, 0.5f), new Vector3(1.2f, 0.45f, 4.2f)),    // cat height
            ("garden", new Vector3(0.6f, 1.4f, 2.5f), new Vector3(-10f, 0.6f, 2.5f)),          // out of the sliding door
            ("court", new Vector3(7.3f, 1.6f, 3.3f), new Vector3(4.9f, 0.2f, 4.8f)),           // the inner court
            ("kitchen", new Vector3(4.9f, 1.1f, 2.2f), new Vector3(7.6f, 0.8f, 2.4f)),         // the front door and the mouse hole
            ("sofa", new Vector3(2.6f, 1.3f, 2.2f), new Vector3(2.6f, 0.4f, 4.5f)),            // the sofa: seat, back and arms
            ("court_window", new Vector3(5.2f, 1.2f, 3.5f), new Vector3(7.6f, 1.2f, 4.0f)),    // the bedroom window and the drainpipe
            ("light_well", new Vector3(7.2f, 0.6f, 3.4f), new Vector3(5.4f, 4.6f, 5.0f)),      // up the light well: windows, sills, gutter
            ("wild_garden", new Vector3(-4.6f, 1.3f, 6.3f), new Vector3(-10.5f, 0.3f, 8.2f)),  // the neighbour's wild garden: barrow, barrel, brambles
            ("dining", new Vector3(3.4f, 1.4f, 2.6f), new Vector3(3.4f, 0.6f, 0.3f)),          // the dining bar by the south wall
            ("sliding_door", new Vector3(1.3f, 1.0f, 2.2f), new Vector3(-0.1f, 0.05f, 2.9f)),  // the sliding door's sill and frames, from above
            ("town_east", new Vector3(4f, 14f, 3f), new Vector3(32f, 5f, 12f)),                // over the roofs: the town to the east
            ("town_west", new Vector3(-2f, 14f, 3f), new Vector3(-34f, 5f, -6f)),              // and to the west, towards the river
            ("street", new Vector3(18.6f, 1.7f, 0f), new Vector3(21.6f, 2.6f, 9f)),           // the street lamps
            ("quay", new Vector3(-33.5f, 1.4f, -20f), new Vector3(-42f, -1f, -60f)),          // the north quay, down the river
            ("footbridge", new Vector3(-33.2f, 1.6f, -82f), new Vector3(-41.5f, 0f, -93f)),   // the footbridge over the river
            ("bakery", new Vector3(-50.5f, 1.3f, -94f), new Vector3(-58.5f, 2.2f, -100f)),     // the bakery's shopfront across the far bank
            ("door_out", new Vector3(-1.4f, 0.45f, 4.6f), new Vector3(0.1f, 0.25f, 2.4f)),     // the open sliding door from the garden, cat height
            ("door_out_b", new Vector3(-0.8f, 0.4f, 1.1f), new Vector3(0.1f, 0.25f, 3.2f)),   // and from the other side
            ("door_in", new Vector3(1.1f, 0.4f, 4.9f), new Vector3(-0.2f, 0.12f, 3.0f)),      // the open door from inside, cat height
            ("door_in_b", new Vector3(1.1f, 0.4f, 0.9f), new Vector3(-0.2f, 0.12f, 2.8f)),    // and from the other end
            ("garden_end", new Vector3(-21.0f, 1.2f, 2.8f), new Vector3(-23.4f, 0.45f, 1.1f)),  // the compost and wheelie bin by the end fence
        };

        /// <summary>The fixed views, plus a side view of the cat (readability).</summary>
        static (string name, Vector3 pos, Vector3 target)[] Views()
        {
            var list = new System.Collections.Generic.List<(string, Vector3, Vector3)>();
            var cat = UnityEngine.Object.FindAnyObjectByType<Draho.Cat.CatMotor>();
            if (cat != null)
            {
                Transform t = cat.transform;
                Debug.Log($"ARTCAPTURE cat at {t.position} facing {t.forward}");
                list.Add(("cat", t.position + t.right * 0.95f + Vector3.up * 0.28f, t.position + Vector3.up * 0.16f));
                // From the rear quarter and above, close to the game camera: the rump and the tail root.
                list.Add(("cat_back", t.position - t.forward * 0.5f + t.right * 0.6f + Vector3.up * 0.38f,
                    t.position + Vector3.up * 0.17f));
                // Close on the face from the front quarter: the fur must leave the eyes and nose clear.
                // (From the cat's left: on its right the kitchen island now stands, and the camera was inside it.)
                list.Add(("cat_face", t.position + t.forward * 0.62f - t.right * 0.25f + Vector3.up * 0.26f,
                    t.position + t.forward * 0.24f + Vector3.up * 0.2f));
            }
            list.AddRange(FixedViews);
            return list.ToArray();
        }

        public static void Run()
        {
            string outDir = Arg("-captureOut") ?? "captures";
            string[] moods = (Arg("-captureMoods") ?? "Day,Evening,Night").Split(',');
            int width = int.Parse(Arg("-captureWidth") ?? "1600"), height = width * 9 / 16;
            Directory.CreateDirectory(outDir);

            PlaygroundBuilder.Build();
            EditorSceneManager.OpenScene(ApartmentBuilder.ScenePath);

            var camGo = new GameObject("ArtCaptureCamera");
            var cam = camGo.AddComponent<Camera>();
            var gameCam = Camera.main;
            if (gameCam != null)
            {
                cam.CopyFrom(gameCam);
                gameCam.enabled = false;
            }
            cam.fieldOfView = 55f;
            cam.nearClipPlane = 0.03f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // SkyMood is a run-time component: start it by hand (Awake reads the scene's day light).
            var sky = new GameObject("SkyMood").AddComponent<SkyMood>();
            typeof(SkyMood).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(sky, null);

            var views = Views();
            PoseCat();
            // By name, so the tool also runs on versions without the rim light (the "before" pictures).
            var rimType = Type.GetType("Draho.Cat.CatRimLight, Assembly-CSharp");
            var rims = rimType != null ? UnityEngine.Object.FindObjectsByType(rimType, FindObjectsSortMode.None) : new UnityEngine.Object[0];
            var rimUpdate = rimType?.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var slidersClosed = new System.Collections.Generic.Dictionary<Transform, Vector3>();
            // Passes: one per mood (-captureMoods), and one per clock hour (-captureHours 7,13,20.5 renders the views in
            // -captureHourViews, default garden and town_west, at those hours: the sun's arc and the moon). The clock
            // stands still while capturing, so the pictures do not depend on how long a frame took.
            SkyMood.ClockRunning = false;
            var passes = new System.Collections.Generic.List<(string label, Action apply, Func<string, bool> want)>();
            foreach (string moodName in moods)
                if (Enum.TryParse(moodName.Trim(), out SkyMood.Mood m))
                    passes.Add((m.ToString(), () => SkyMood.Set(m, 0f), _ => true));
            string hourArg = Arg("-captureHours");
            if (!string.IsNullOrEmpty(hourArg))
            {
                var hourViews = (Arg("-captureHourViews") ?? "garden,town_west").Split(',');
                var testHour = typeof(SkyMood).GetMethod("TestHour");
                foreach (string hs in hourArg.Split(','))
                    if (float.TryParse(hs, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h))
                        passes.Add(($"{h:00.00}h".Replace(',', '.'), () => testHour?.Invoke(sky, new object[] { h }), n => Array.IndexOf(hourViews, n) >= 0));
            }
            foreach (var pass in passes)
            {
                pass.apply();
                foreach (var v in views)
                {
                    if (!pass.want(v.name)) continue;
                    // "door_" views: the sliding door open (its sash moved by the door's own openOffset), else closed.
                    foreach (var sd in UnityEngine.Object.FindObjectsByType<Draho.World.SlidingDoor>(FindObjectsSortMode.None))
                    {
                        var f = typeof(Draho.World.SlidingDoor).GetField("slider", BindingFlags.Instance | BindingFlags.NonPublic);
                        var o = typeof(Draho.World.SlidingDoor).GetField("openOffset", BindingFlags.Instance | BindingFlags.NonPublic);
                        var slider = f?.GetValue(sd) as Transform;
                        if (slider == null || o == null) continue;
                        if (!slidersClosed.ContainsKey(slider)) slidersClosed[slider] = slider.position;
                        slider.position = slidersClosed[slider] + (v.name.StartsWith("door_") ? (Vector3)o.GetValue(sd) : Vector3.zero);
                    }
                    camGo.transform.position = v.pos;
                    camGo.transform.rotation = Quaternion.LookRotation(v.target - v.pos);
                    // The rim light follows the game camera at run time: put that camera here and let it update.
                    if (gameCam != null) gameCam.transform.SetPositionAndRotation(camGo.transform.position, camGo.transform.rotation);
                    foreach (var rim in rims) rimUpdate?.Invoke(rim, null);
                    // The indoor light (window, lamps, dimmer ambient inside) for a viewer at the camera, then the sky's
                    // ambient again with it. By name, so the tool also runs on versions without IndoorLight.
                    var indoorType = Type.GetType("Draho.World.IndoorLight, Assembly-CSharp");
                    var indoor = indoorType != null ? UnityEngine.Object.FindAnyObjectByType(indoorType) : null;
                    indoorType?.GetMethod("Refresh")?.Invoke(indoor, new object[] { v.pos, true });
                    typeof(SkyMood).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(sky, null);
                    // The clouds take their colour from the mood in SkyClouds.Update (run time only): without it they
                    // stayed daytime white in the evening and night pictures.
                    var cloudsType = Type.GetType("Draho.World.SkyClouds, Assembly-CSharp");
                    var cloudsUpdate = cloudsType?.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (cloudsType != null)
                        foreach (var c in UnityEngine.Object.FindObjectsByType(cloudsType, FindObjectsSortMode.None))
                        {
                            var matField = cloudsType.GetField("material", BindingFlags.Instance | BindingFlags.NonPublic);
                            if (matField != null && matField.GetValue(c) == null) // Awake once: it makes the material instance
                                cloudsType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);
                            cloudsUpdate?.Invoke(c, null);
                        }
                    // The street, quay and garden lamps and the bakery display (NightLamp) switch with the time of day at
                    // run time only: run them too, so the evening and night pictures show them lit.
                    var lampUpdate = typeof(Draho.World.NightLamp).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
                    foreach (var lamp in UnityEngine.Object.FindObjectsByType<Draho.World.NightLamp>(FindObjectsSortMode.None))
                        lampUpdate?.Invoke(lamp, null);
                    // Twice: the first frame after a change can miss state (the first picture came out red).
                    for (int renderPass = 0; renderPass < 2; renderPass++)
                    {
                        var request = new RenderPipeline.StandardRequest { destination = rt };
                        if (RenderPipeline.SupportsRenderRequest(cam, request))
                            RenderPipeline.SubmitRenderRequest(cam, request);
                        else
                        {
                            cam.targetTexture = rt;
                            cam.Render();
                            cam.targetTexture = null;
                        }
                    }
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prev;
                    string file = Path.Combine(outDir, $"{v.name}_{pass.label}.png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    Debug.Log($"ARTCAPTURE {file}");
                }
            }
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>
        /// Animal review sheet: one of each animal on a neutral floor high above the town, next to Drahomil for scale,
        /// from three angles in the day light. Files: animal_[Type]_[n]_[angle].png in -captureOut.
        /// </summary>
        public static void Animals()
        {
            string outDir = Arg("-captureOut") ?? "captures/animals";
            Directory.CreateDirectory(outDir);
            PlaygroundBuilder.Build();
            EditorSceneManager.OpenScene(ApartmentBuilder.ScenePath);

            var cam = new GameObject("ArtCaptureCamera").AddComponent<Camera>();
            if (Camera.main != null) { cam.CopyFrom(Camera.main); Camera.main.enabled = false; }
            cam.nearClipPlane = 0.01f;
            cam.fieldOfView = 35f;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            PoseCat();

            Vector3 studio = new Vector3(0f, 200f, 0f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = studio;
            floor.transform.localScale = Vector3.one * 2f;
            floor.GetComponent<Renderer>().sharedMaterial = PlaygroundBuilder.LitMaterial("Studio_Floor", PaletteMap.Named("warm_grey", Color.grey), 0.1f);

            var catMotor = UnityEngine.Object.FindAnyObjectByType<Draho.Cat.CatMotor>();
            string[] types = { "Mouse", "Hedgehog", "Fox", "Bird", "Pigeon", "Duck", "Tortoise", "Moth", "Hrozna", "Fatsocat", "NpcCat" };
            int width = 900, height = 600;
            var rt = new RenderTexture(width, height, 24);
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            foreach (string typeName in types)
            {
                var type = Type.GetType($"Draho.World.{typeName}, Assembly-CSharp");
                if (type == null) { Debug.LogWarning($"ARTCAPTURE no type {typeName}"); continue; }
                var found = UnityEngine.Object.FindObjectsByType(type, FindObjectsSortMode.InstanceID);
                if (typeName == "Duck") // a drake and a hen
                    found = found.OrderBy(o => AnimalModels.Find(((Component)o).transform, "Duck_Hen_Body") != null ? 0 : 1)
                        .Where((o, i) => i == 0 || AnimalModels.Find(((Component)o).transform, "Duck_Hen_Body") == null)
                        .Take(2).ToArray();
                if (typeName == "Bird") // a male blackbird and the hen
                    found = found.OrderBy(o => AnimalModels.Find(((Component)o).transform, "Blackbird_Hen_Body") != null ? 1 : 0)
                        .Where((o, i) => i == 0 || AnimalModels.Find(((Component)o).transform, "Blackbird_Hen_Body") != null)
                        .ToArray();
                for (int n = 0; n < Mathf.Min(found.Length, typeName == "Pigeon" ? 3 : typeName == "Duck" || typeName == "Bird" ? 2 : 1); n++)
                {
                    var animal = ((Component)found[n]).transform;
                    Vector3 oldPos = animal.position;
                    Quaternion oldRot = animal.rotation;
                    animal.SetPositionAndRotation(studio, Quaternion.Euler(0f, 0f, 0f));
                    var rends = animal.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
                    if (rends.Length == 0) { animal.SetPositionAndRotation(oldPos, oldRot); continue; }
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    // Drahomil beside it, for scale.
                    Vector3 catOld = catMotor != null ? catMotor.transform.position : Vector3.zero;
                    if (catMotor != null) catMotor.transform.SetPositionAndRotation(studio + new Vector3(Mathf.Max(b.extents.x, 0.15f) + 0.35f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f));
                    float size = Mathf.Max(b.size.magnitude, 0.25f);
                    var wingL = AnimalModels.Find(animal, "WingL");
                    var wingR = AnimalModels.Find(animal, "WingR");
                    var angles = new System.Collections.Generic.List<(string name, Vector3 dir)> { ("front", new Vector3(-0.6f, 0.35f, 1f)), ("side", new Vector3(-1f, 0.15f, 0f)), ("top", new Vector3(-0.4f, 1.2f, 0.6f)) };
                    if (wingL != null && wingR != null) angles.Add(("flap", new Vector3(-0.25f, 0.45f, 1f)));
                    foreach (var a in angles)
                    {
                        if (a.name == "flap") // the top of the wing beat (Bird and Pigeon swing up to about 100 degrees)
                        {
                            wingL.localRotation = Quaternion.Euler(0f, 0f, -100f);
                            wingR.localRotation = Quaternion.Euler(0f, 0f, 100f);
                        }
                        Vector3 target = b.center;
                        cam.transform.position = target + a.dir.normalized * (size * 1.9f + 0.25f);
                        cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position);
                        for (int pass = 0; pass < 2; pass++)
                            RenderPipeline.SubmitRenderRequest(cam, new RenderPipeline.StandardRequest { destination = rt });
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tex.Apply();
                        RenderTexture.active = prev;
                        File.WriteAllBytes(Path.Combine(outDir, $"animal_{typeName}_{n}_{a.name}.png"), tex.EncodeToPNG());
                    }
                    if (wingL != null && wingR != null) wingL.localRotation = wingR.localRotation = Quaternion.identity;
                    int tris = animal.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).Sum(f => (int)f.sharedMesh.GetIndexCount(0) / 3);
                    // Every Transform the animal's script animates must be wired (a wrong part name would leave it empty).
                    var fields = new SerializedObject((Component)found[n]).GetIterator();
                    int wired = 0, empty = 0;
                    while (fields.NextVisible(true))
                    {
                        if (fields.propertyType != SerializedPropertyType.ObjectReference || !fields.type.Contains("Transform")) continue;
                        if (fields.objectReferenceValue != null) wired++;
                        else { empty++; Debug.LogWarning($"ARTCAPTURE EMPTY PART {typeName}.{fields.propertyPath}"); }
                    }
                    Debug.Log($"ARTCAPTURE parts {typeName} #{n}: {wired} wired, {empty} empty");
                    Debug.Log($"ARTCAPTURE animal {typeName} #{n} '{animal.name}': {rends.Length} renderers, ~{tris} triangles, size {b.size:F2} m");
                    animal.SetPositionAndRotation(oldPos, oldRot);
                    if (catMotor != null) catMotor.transform.position = catOld;
                }
            }
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>The Animator does not run in edit mode: put the cat in the first frame of its Idle clip.</summary>
        static void PoseCat()
        {
            // Drahomil and every other rigged animal (Fatsocat, the neighbourhood cats): without a sampled clip a
            // skinned mesh shows its bind pose in edit mode (the cats stood upright like people).
            foreach (var animator in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                if (animator.runtimeAnimatorController == null) continue;
                foreach (var clip in animator.runtimeAnimatorController.animationClips)
                {
                    if (clip.name != "Idle") continue;
                    clip.SampleAnimation(animator.gameObject, 0f);
                    Debug.Log($"ARTCAPTURE posed {animator.name}: Idle");
                    break;
                }
            }
        }

        static string Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
