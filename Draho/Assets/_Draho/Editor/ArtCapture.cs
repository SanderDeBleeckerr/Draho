using System;
using System.IO;
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
            foreach (string moodName in moods)
            {
                if (!Enum.TryParse(moodName.Trim(), out SkyMood.Mood mood)) continue;
                SkyMood.Set(mood, 0f);
                foreach (var v in views)
                {
                    camGo.transform.position = v.pos;
                    camGo.transform.rotation = Quaternion.LookRotation(v.target - v.pos);
                    // The rim light follows the game camera at run time: put that camera here and let it update.
                    if (gameCam != null) gameCam.transform.SetPositionAndRotation(camGo.transform.position, camGo.transform.rotation);
                    foreach (var rim in rims) rimUpdate?.Invoke(rim, null);
                    // Twice: the first frame after a change can miss state (the first picture came out red).
                    for (int pass = 0; pass < 2; pass++)
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
                    string file = Path.Combine(outDir, $"{v.name}_{mood}.png");
                    File.WriteAllBytes(file, tex.EncodeToPNG());
                    Debug.Log($"ARTCAPTURE {file}");
                }
            }
            UnityEngine.Object.DestroyImmediate(rt);
        }

        /// <summary>The Animator does not run in edit mode: put the cat in the first frame of its Idle clip.</summary>
        static void PoseCat()
        {
            var cat = UnityEngine.Object.FindAnyObjectByType<Draho.Cat.CatMotor>();
            var animator = cat != null ? cat.GetComponentInChildren<Animator>() : null;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name != "Idle") continue;
                clip.SampleAnimation(animator.gameObject, 0f);
                Debug.Log("ARTCAPTURE cat posed: Idle");
                return;
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
