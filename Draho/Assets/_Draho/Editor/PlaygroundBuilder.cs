using System.IO;
using System.Linq;
using Draho.CameraRig;
using Draho.Cat;
using Draho.Controls;
using Draho.Core;
using Draho.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Draho.EditorTools
{
    /// <summary>
    /// Menu "Draho/Build Scenes": creates every asset, the apartment level and the playground test scene
    /// from code, so the repository does not need hand-made scene files. Safe to run again (it rebuilds).
    /// </summary>
    public static class PlaygroundBuilder
    {
        const string Root = "Assets/_Draho";
        const string CatFbx = Root + "/Art/Cat/Drahomil.fbx";
        internal const string ArtDir = Root + "/Art";
        internal const string MaterialDir = Root + "/Art/Materials";
        const string SettingsDir = Root + "/Settings";
        const string PrefabDir = Root + "/Prefabs";
        public const string ScenePath = Root + "/Scenes/Playground.unity";
        const int IgnoreRaycastLayer = 2;
        const float FloorSize = 20f; // meters; walk past the edge to fall off the world

        [MenuItem("Draho/Build Scenes")]
        static void BuildFromMenu()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) Build();
        }

        /// <summary>Creates all assets and both scenes. Also called by the CI build (no dialogs).</summary>
        public static void Build()
        {
            GameObject catPrefab = PrepareCat();
            BuildScene(catPrefab);
            ApartmentBuilder.Build(catPrefab);
            // The apartment is the first scene, so the web build opens it.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ApartmentBuilder.ScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("Draho: scenes ready. Press Play. WASD/arrows + Shift, right mouse to orbit.");
        }

        /// <summary>Project settings, cat stats, animator and the cat prefab.</summary>
        static GameObject PrepareCat()
        {
            foreach (var dir in new[] { MaterialDir, SettingsDir, PrefabDir, ArtDir + "/Textures", Path.GetDirectoryName(ScenePath) })
                Directory.CreateDirectory(dir);
            AssetDatabase.Refresh(); // the asset database must know the new folders

            EnsureUrp();
            EnsureInputSystemActive();
            ConfigurePlayer();
            CatStats stats = LoadOrCreate<CatStats>(SettingsDir + "/CatStats.asset");
            // The clip speeds belong to the cat model; keep them in sync with the code defaults.
            var defaults = ScriptableObject.CreateInstance<CatStats>();
            stats.walkClipSpeed = defaults.walkClipSpeed;
            stats.runClipSpeed = defaults.runClipSpeed;
            stats.crouchClipSpeed = defaults.crouchClipSpeed;
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(stats);
            AnimatorController controller = BuildAnimator(stats);
            return BuildCatPrefab(stats, controller);
        }

        // ------------------------------------------------------------------ project
        static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return;
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(rendererData, SettingsDir + "/Mobile_Renderer.asset");
            var urp = UniversalRenderPipelineAsset.Create(rendererData);
            urp.renderScale = 1f;
            urp.msaaSampleCount = 2;
            urp.shadowDistance = 15f;
            AssetDatabase.CreateAsset(urp, SettingsDir + "/Mobile_URP.asset");
            GraphicsSettings.defaultRenderPipeline = urp;
            QualitySettings.renderPipeline = urp;
            Debug.Log("Draho: created and assigned a URP asset.");
        }

        /// <summary>Player Settings > Active Input Handling = "Input System Package (New)".</summary>
        static void EnsureInputSystemActive()
        {
            var settings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            var so = new SerializedObject(settings);
            var handler = so.FindProperty("activeInputHandler");
            if (handler == null || handler.intValue == 1) return;
            handler.intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings); // so AssetDatabase.SaveAssets writes ProjectSettings.asset
            Debug.LogWarning("Draho: switched Active Input Handling to the Input System. Restart the editor.");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Draho";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        // ------------------------------------------------------------------ assets
        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        internal static Material LitMaterial(string name, Color color, float smoothness, Texture texture = null,
            Vector2? tiling = null)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            if (texture != null)
            {
                mat.SetTexture("_BaseMap", texture);
                mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D CheckerTexture()
        {
            string path = ArtDir + "/Textures/FloorChecker.png";
            if (!File.Exists(path))
            {
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                var a = new Color(0.82f, 0.80f, 0.76f);
                var b = new Color(0.70f, 0.68f, 0.64f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    tex.SetPixel(x, y, ((x / (size / 2)) + (y / (size / 2))) % 2 == 0 ? a : b);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static AnimatorController BuildAnimator(CatStats stats)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(CatFbx).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__")).ToDictionary(c => c.name);
            AnimationClip Clip(string n) => clips.TryGetValue(n, out var c)
                ? c
                : throw new System.Exception($"Draho: clip '{n}' not found in {CatFbx}. Found: {string.Join(", ", clips.Keys)}");

            string path = ArtDir + "/Cat/Drahomil.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Airborne", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);

            // Standing: Idle / Walk / Run on Speed. Walk and Run play faster so the paws match the speed.
            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(Clip("Idle"), 0f);
            tree.AddChild(Clip("Walk"), stats.walkSpeed);
            tree.AddChild(Clip("Run"), stats.runSpeed);
            var children = tree.children;
            children[1].timeScale = stats.walkSpeed / stats.walkClipSpeed;
            children[2].timeScale = stats.runSpeed / stats.runClipSpeed;
            tree.children = children;

            // Crouched: CrouchIdle / CrouchWalk on Speed.
            AnimatorState crouch = controller.CreateBlendTreeInController("Crouch", out BlendTree crouchTree, 0);
            crouchTree.blendType = BlendTreeType.Simple1D;
            crouchTree.blendParameter = "Speed";
            crouchTree.useAutomaticThresholds = false;
            crouchTree.AddChild(Clip("CrouchIdle"), 0f);
            crouchTree.AddChild(Clip("CrouchWalk"), stats.crouchSpeed);
            children = crouchTree.children;
            children[1].timeScale = stats.crouchSpeed / stats.crouchClipSpeed;
            crouchTree.children = children;

            // In the air: Jump pose while rising, Fall pose (paws reaching down) while falling.
            AnimatorState air = controller.CreateBlendTreeInController("Air", out BlendTree airTree, 0);
            airTree.blendType = BlendTreeType.Simple1D;
            airTree.blendParameter = "VerticalSpeed";
            airTree.useAutomaticThresholds = false;
            airTree.AddChild(Clip("Fall"), -2.5f);
            airTree.AddChild(Clip("Jump"), 1.5f);

            AnimatorStateTransition Link(AnimatorState from, AnimatorState to, float duration,
                params (AnimatorConditionMode mode, string parameter)[] conditions)
            {
                var t = from.AddTransition(to);
                t.hasExitTime = false;
                t.duration = duration;
                foreach (var (mode, parameter) in conditions) t.AddCondition(mode, 0f, parameter);
                return t;
            }
            Link(locomotion, crouch, 0.15f, (AnimatorConditionMode.If, "Crouch"), (AnimatorConditionMode.IfNot, "Airborne"));
            Link(crouch, locomotion, 0.15f, (AnimatorConditionMode.IfNot, "Crouch"));
            Link(locomotion, air, 0.1f, (AnimatorConditionMode.If, "Airborne"));
            Link(crouch, air, 0.1f, (AnimatorConditionMode.If, "Airborne"));
            Link(air, locomotion, 0.12f, (AnimatorConditionMode.IfNot, "Airborne"), (AnimatorConditionMode.IfNot, "Crouch"));
            Link(air, crouch, 0.12f, (AnimatorConditionMode.IfNot, "Airborne"), (AnimatorConditionMode.If, "Crouch"));
            return controller;
        }

        static GameObject BuildCatPrefab(CatStats stats, AnimatorController controller)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CatFbx);
            var root = new GameObject("Drahomil");
            var cc = root.AddComponent<CharacterController>();
            cc.radius = 0.11f;
            cc.height = 0.26f;
            cc.center = new Vector3(0f, 0.13f, 0f);
            cc.stepOffset = 0.05f;
            cc.skinWidth = 0.01f;
            cc.minMoveDistance = 0f;
            cc.slopeLimit = 50f;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "Model";
            FixModelOrientationAndScale(model);

            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            var materials = new[]
            {
                LitMaterial("Fur", new Color(0.12f, 0.12f, 0.135f), 0.35f),
                LitMaterial("Eye", Color.white, 0.85f, CatTexture("CatEye")),   // iris, pupil, highlight
                LitMaterial("Pupil", Color.black, 0.9f),                       // lid line and mouth
                LitMaterial("Nose", Color.white, 0.5f, CatTexture("CatNose")),  // leather and nostrils
                LitMaterial("InnerEar", new Color(0.36f, 0.22f, 0.25f), 0.2f),
                LitMaterial("Whisker", new Color(0.82f, 0.82f, 0.8f), 0.4f),
            }.ToDictionary(m => m.name);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = r.sharedMaterials
                    .Select(m => m != null && materials.TryGetValue(m.name, out var ours) ? ours : m).ToArray();
                r.shadowCastingMode = ShadowCastingMode.On;
            }

            var motor = root.AddComponent<CatMotor>();
            SetField(motor, "stats", stats);
            var catAnimator = root.AddComponent<CatAnimator>();
            SetField(catAnimator, "motor", motor);
            SetField(catAnimator, "animator", animator);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = IgnoreRaycastLayer;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/Drahomil.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Face textures painted by Tools/Blender/convert_lowpoly_cat.py (clamped: they cover one part each).</summary>
        static Texture2D CatTexture(string name)
        {
            string path = $"{ArtDir}/Cat/Textures/{name}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Safety net: the cat must face +Z and be about 0.3 m high.</summary>
        static void FixModelOrientationAndScale(GameObject model)
        {
            var head = model.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Head");
            if (head != null && model.transform.InverseTransformPoint(head.position).z < 0f)
            {
                model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                Debug.LogWarning("Draho: cat model faced backwards; rotated it 180°.");
            }
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            if (bounds.size.y < 0.15f || bounds.size.y > 0.7f)
            {
                model.transform.localScale *= 0.35f / bounds.size.y;
                Debug.LogWarning($"Draho: cat model was {bounds.size.y:0.###} m high; rescaled to 0.35 m.");
            }
        }

        // ------------------------------------------------------------------ scene
        static void BuildScene(GameObject catPrefab)
        {
            Scene scene = NewLitScene(Quaternion.Euler(50f, -30f, 0f));

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // 10 x 10 m at scale 1
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(FloorSize / 10f, 1f, FloorSize / 10f);
            floor.GetComponent<Renderer>().sharedMaterial =
                LitMaterial("Floor", Color.white, 0.15f, CheckerTexture(), Vector2.one * FloorSize / 2f);

            var spawn = new GameObject("SpawnPoint").transform;
            spawn.position = new Vector3(0f, 0.02f, 0f);
            AddPlayerRig(scene, catPrefab, spawn, new Color(0.55f, 0.65f, 0.8f));

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>New empty scene with ambient light, a sun and the game bootstrap.</summary>
        internal static Scene NewLitScene(Quaternion sunRotation)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.45f, 0.45f, 0.47f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.19f);

            new GameObject("Game").AddComponent<GameBootstrap>();

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = sunRotation;
            return scene;
        }

        /// <summary>The cat at the spawn point, touch controls, the orbit camera and the fall respawn.</summary>
        internal static void AddPlayerRig(Scene scene, GameObject catPrefab, Transform spawn, Color background)
        {
            var cat = (GameObject)PrefabUtility.InstantiatePrefab(catPrefab, scene);
            cat.transform.SetPositionAndRotation(spawn.position, spawn.rotation);

            // UI: left half = joystick, right half = camera swipe.
            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var stickArea = UiRect("JoystickArea", canvasGo.transform, Vector2.zero, new Vector2(0.5f, 1f));
            stickArea.pivot = Vector2.zero;
            var stickBg = UiRect("Background", stickArea, Vector2.zero, Vector2.zero, circle, new Color(1, 1, 1, 0.22f));
            stickBg.sizeDelta = Vector2.one * 220f;
            stickBg.anchoredPosition = new Vector2(260f, 260f);
            var knob = UiRect("Knob", stickBg, Vector2.one * 0.5f, Vector2.one * 0.5f, circle, new Color(1, 1, 1, 0.55f));
            knob.sizeDelta = Vector2.one * 100f;
            knob.GetComponent<Image>().raycastTarget = false;
            stickBg.GetComponent<Image>().raycastTarget = false;
            var joystick = stickArea.gameObject.AddComponent<VirtualJoystick>();
            SetField(joystick, "background", stickBg);
            SetField(joystick, "knob", knob);
            SetField(joystick, "radius", 110f);

            var lookArea = UiRect("LookArea", canvasGo.transform, new Vector2(0.5f, 0f), Vector2.one)
                .gameObject.AddComponent<TouchLookArea>();

            // Buttons after the look area, so they get the touch first. Bottom-right, under the right thumb.
            TouchButton jumpButton = HudButton(canvasGo.transform, "JumpButton", "JUMP", new Vector2(-230f, 230f), 200f, false, circle);
            TouchButton crouchButton = HudButton(canvasGo.transform, "CrouchButton", "DUCK", new Vector2(-450f, 140f), 150f, true, circle);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var inputGo = new GameObject("PlayerInput");
            var input = inputGo.AddComponent<TouchCatInput>();
            SetField(input, "joystick", joystick);
            SetField(input, "jumpButton", jumpButton);
            SetField(input, "crouchButton", crouchButton);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 200f;
            cam.fieldOfView = 55f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            camGo.transform.position = spawn.position + new Vector3(0f, 0.6f, -1.2f);
            var orbit = camGo.AddComponent<OrbitCamera>();
            SetField(orbit, "target", cat.transform);
            SetField(orbit, "lookArea", lookArea);

            var motor = cat.GetComponent<CatMotor>();
            SetField(motor, "inputSource", input);
            SetField(motor, "cameraTransform", camGo.transform);

            var respawn = new GameObject("FallRespawn").AddComponent<FallRespawn>();
            SetField(respawn, "motor", motor);
            SetField(respawn, "spawnPoint", spawn);
        }

        static TouchButton HudButton(Transform canvas, string name, string label, Vector2 fromBottomRight, float size,
            bool toggle, Sprite circle)
        {
            var rt = UiRect(name, canvas, Vector2.right, Vector2.right, circle, new Color(1f, 1f, 1f, 0.35f));
            rt.sizeDelta = Vector2.one * size;
            rt.anchoredPosition = fromBottomRight;
            var text = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var textRt = (RectTransform)text.transform;
            textRt.SetParent(rt, false);
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;
            var t = text.GetComponent<Text>();
            t.text = label;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = Mathf.RoundToInt(size * 0.2f);
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = new Color(0.1f, 0.1f, 0.12f, 0.85f);
            t.raycastTarget = false;
            var button = rt.gameObject.AddComponent<TouchButton>();
            SetField(button, "toggle", toggle);
            SetField(button, "icon", rt.GetComponent<Image>());
            return button;
        }

        static RectTransform UiRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
            Sprite sprite = null, Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color ?? new Color(0, 0, 0, 0); // invisible, but still receives touches
            return rt;
        }

        static void SetField(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field) ?? throw new System.Exception($"Draho: no field '{field}' on {target.GetType().Name}");
            switch (value)
            {
                case Object o: prop.objectReferenceValue = o; break;
                case float f: prop.floatValue = f; break;
                case int i: prop.intValue = i; break;
                case bool b: prop.boolValue = b; break;
                default: throw new System.ArgumentException(value?.GetType().Name);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
