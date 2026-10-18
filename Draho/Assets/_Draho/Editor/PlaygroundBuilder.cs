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
            stats.climbClipSpeed = defaults.climbClipSpeed;
            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(stats);
            AnimatorController controller = BuildAnimator(stats);
            return BuildCatPrefab(stats, controller);
        }

        // ------------------------------------------------------------------ project
        static void EnsureUrp()
        {
            var urp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, SettingsDir + "/Mobile_Renderer.asset");
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, SettingsDir + "/Mobile_URP.asset");
                GraphicsSettings.defaultRenderPipeline = urp;
                QualitySettings.renderPipeline = urp;
                Debug.Log("Draho: created and assigned a URP asset.");
            }
            urp.renderScale = 1f;
            // When AdaptiveQuality lowers the render scale, AMD FSR 1 upscales more sharply than bilinear
            // (Unity manual: "Introduction to changing resolution scale", URP supports FSR 1).
            urp.upscalingFilter = UpscalingFilterSelection.FSR;
            urp.fsrOverrideSharpness = true;
            urp.fsrSharpness = 0.8f;
            urp.msaaSampleCount = 2;
            urp.shadowDistance = 10f;    // the apartment is small: spend the shadow map on it
            urp.shadowCascadeCount = 2;
            // Soft shadows and the shadow map size have no public setter in every URP version.
            var so = new SerializedObject(urp);
            SetIfFound(so, "m_MainLightShadowmapResolution", 2048);
            SetIfFound(so, "m_SoftShadowsSupported", true);
            SetIfFound(so, "m_SupportsHDR", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            // A renderer made from code can miss its post-processing data; then post effects do not render.
            var rendererAsset = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(SettingsDir + "/Mobile_Renderer.asset");
            if (rendererAsset != null && rendererAsset.postProcessData == null)
            {
                rendererAsset.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                Debug.Log($"Draho: post-process data assigned: {rendererAsset.postProcessData != null}");
                EditorUtility.SetDirty(rendererAsset);
            }
        }

        static void SetIfFound(SerializedObject so, string name, object value)
        {
            var prop = so.FindProperty(name);
            if (prop == null) { Debug.LogWarning($"Draho: URP setting '{name}' not found."); return; }
            if (value is bool b) prop.boolValue = b;
            else prop.intValue = (int)value;
        }

        /// <summary>Filmic look: neutral tone mapping, a soft bloom on the lights and a light vignette.</summary>
        static VolumeProfile PostProfile()
        {
            string path = SettingsDir + "/PostProcess.asset";
            // Both scenes use this asset, so refill it instead of making a new one.
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            foreach (var old in profile.components.ToArray()) Object.DestroyImmediate(old, true);
            profile.components.Clear();

            T Add<T>() where T : VolumeComponent
            {
                var c = profile.Add<T>(true);
                c.name = typeof(T).Name;
                AssetDatabase.AddObjectToAsset(c, profile);
                return c;
            }
            Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var bloom = Add<Bloom>();
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            var color = Add<ColorAdjustments>();
            color.postExposure.Override(0.15f);
            color.contrast.Override(8f);
            color.saturation.Override(6f);
            var vignette = Add<Vignette>();
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            EditorUtility.SetDirty(profile);
            return profile;
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
            PlayerSettings.WebGL.template = "PROJECT:Draho"; // Assets/WebGLTemplates/Draho: full screen
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
            controller.AddParameter("Rear", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Climb", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ClimbSpeed", AnimatorControllerParameterType.Float);

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

            // Standing up on the hind legs, and climbing (from any state; the clip speed follows the climb speed).
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState rear = machine.AddState("Rear");
            rear.motion = Clip("Rear");
            AnimatorState climb = machine.AddState("Climb");
            climb.motion = Clip("Climb");
            climb.speedParameter = "ClimbSpeed";
            climb.speedParameterActive = true;
            Link(locomotion, rear, 0.25f, (AnimatorConditionMode.If, "Rear"));
            Link(rear, locomotion, 0.3f, (AnimatorConditionMode.IfNot, "Rear"), (AnimatorConditionMode.IfNot, "Climb"));
            var toClimb = machine.AddAnyStateTransition(climb);
            toClimb.hasExitTime = false;
            toClimb.duration = 0.2f;
            toClimb.canTransitionToSelf = false;
            toClimb.AddCondition(AnimatorConditionMode.If, 0f, "Climb");
            Link(climb, locomotion, 0.2f, (AnimatorConditionMode.IfNot, "Climb"), (AnimatorConditionMode.IfNot, "Airborne"));
            Link(climb, air, 0.1f, (AnimatorConditionMode.IfNot, "Climb"), (AnimatorConditionMode.If, "Airborne"));
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

            // Eyeshine at night (EyeShine drives the emission; black by day).
            var eyeMat = materials["Eye"];
            eyeMat.EnableKeyword("_EMISSION");
            eyeMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            eyeMat.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(eyeMat);
            SetField(root.AddComponent<EyeShine>(), "eye", eyeMat);

            var motor = root.AddComponent<CatMotor>();
            SetField(motor, "stats", stats);
            var catAnimator = root.AddComponent<CatAnimator>();
            SetField(catAnimator, "motor", motor);
            SetField(catAnimator, "animator", animator);
            SetField(catAnimator, "dust", DustPuff(root.transform));

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
        /// <summary>
        /// Another cat (a character, not the player): the Drahomil model with its own fur colour and size,
        /// the same animator (it idles), no motor. Used by the level builder.
        /// </summary>
        internal static GameObject NpcCatModel(string name, Transform parent, Color fur, float scale)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CatFbx);
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            FixModelOrientationAndScale(model);
            model.transform.localScale *= scale;
            var animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ArtDir + "/Cat/Drahomil.controller");
            animator.applyRootMotion = false;
            var furMat = LitMaterial($"Fur_{name}", fur, 0.3f);
            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m =>
                    m == null ? m : m.name == "Fur" ? furMat : AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/{m.name}.mat") ?? m).ToArray();
            return root;
        }

        /// <summary>A small dust puff for hard landings: soft round particles, emitted by CatAnimator.</summary>
        static ParticleSystem DustPuff(Transform parent)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = new Color(0.82f, 0.77f, 0.7f, 0.55f);
            main.gravityModifier = -0.05f;
            main.maxParticles = 40;
            var emission = ps.emission;
            emission.enabled = false; // only explicit Emit() calls
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var fade = ps.colorOverLifetime;
            fade.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;
            var grow = ps.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            string path = $"{MaterialDir}/Dust.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetFloat("_Surface", 1f); // transparent, alpha blend
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            EditorUtility.SetDirty(mat);
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        static void FixModelOrientationAndScale(GameObject model)
        {
            var bones = model.GetComponentsInChildren<Transform>();
            var head = bones.FirstOrDefault(t => t.name == "Head");
            var tail = bones.FirstOrDefault(t => t.name == "Tail1");
            float headZ = head != null ? model.transform.InverseTransformPoint(head.position).z : 0f;
            float tailZ = tail != null ? model.transform.InverseTransformPoint(tail.position).z : 0f;
            if (head != null && tail != null && headZ < tailZ)
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
            // Warm afternoon bounce light from the ground and the horizon (low-poly look: the gradient and the
            // palette do much of the lighting work).
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.49f, 0.45f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.26f, 0.21f);

            new GameObject("Game").AddComponent<GameBootstrap>();

            // A soft gradient sky (Unity's procedural skybox), also used for the ambient light colour.
            string skyPath = MaterialDir + "/Sky.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            var skyShader = Shader.Find("Skybox/Procedural");
            if (sky == null && skyShader != null)
            {
                sky = new Material(skyShader);
                AssetDatabase.CreateAsset(sky, skyPath);
            }
            if (sky != null)
            {
                sky.SetFloat("_SunSize", 0.03f);
                sky.SetFloat("_AtmosphereThickness", 0.8f);
                sky.SetColor("_SkyTint", new Color(0.55f, 0.65f, 0.8f));
                sky.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.38f));
                sky.SetFloat("_Exposure", 1.15f);
                EditorUtility.SetDirty(sky);
                RenderSettings.skybox = sky;
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = sunRotation;
            return scene;
        }

        /// <summary>The cat at the spawn point, touch controls, the orbit camera and the fall respawn.</summary>
        /// <returns>The HUD canvas, for more UI.</returns>
        internal static Transform AddPlayerRig(Scene scene, GameObject catPrefab, Transform spawn, Color background)
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

            // A tap on the stick jumps. A hint under the stick says so.
            var hint = new GameObject("TapHint", typeof(RectTransform), typeof(Text));
            var hintRt = (RectTransform)hint.transform;
            hintRt.SetParent(stickArea, false);
            hintRt.anchorMin = hintRt.anchorMax = Vector2.zero;
            hintRt.sizeDelta = new Vector2(440f, 50f);
            hintRt.anchoredPosition = new Vector2(260f, 115f);
            var hintText = hint.GetComponent<Text>();
            hintText.text = "tap = jump · hold = climb";
            hintText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hintText.fontSize = 30;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = new Color(1f, 1f, 1f, 0.45f);
            hintText.raycastTarget = false;

            // Buttons after the look area, so they get the touch first. Bottom-right, under the right thumb.
            TouchButton crouchButton = HudButton(canvasGo.transform, "CrouchButton", "DUCK", new Vector2(-230f, 210f), 180f, true, circle);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var inputGo = new GameObject("PlayerInput");
            var input = inputGo.AddComponent<TouchCatInput>();
            SetField(input, "joystick", joystick);
            SetField(input, "crouchButton", crouchButton);

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 170f; // fog (apartment scene) hides the end of the view
            cam.fieldOfView = 55f;
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            camGo.transform.position = spawn.position + new Vector3(0f, 0.6f, -1.2f);
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var volume = new GameObject("PostProcess").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();
            // A soft fill light that moves with the camera: the black fur stays readable on dark ivy
            // and in shade. Short range, so walls further away hardly change.
            var fill = new GameObject("CatFillLight").AddComponent<Light>();
            fill.transform.SetParent(camGo.transform, false);
            fill.transform.localPosition = new Vector3(0.15f, 0.25f, 0f);
            fill.type = LightType.Point;
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.intensity = 0.45f;
            fill.range = 1.6f;
            fill.shadows = LightShadows.None;
            camGo.AddComponent<AdaptiveQuality>();
            var orbit = camGo.AddComponent<OrbitCamera>();
            SetField(orbit, "target", cat.transform);
            SetField(orbit, "lookArea", lookArea);

            var motor = cat.GetComponent<CatMotor>();
            SetField(motor, "inputSource", input);
            SetField(motor, "cameraTransform", camGo.transform);

            var respawn = new GameObject("FallRespawn").AddComponent<FallRespawn>();
            SetField(respawn, "motor", motor);
            SetField(respawn, "spawnPoint", spawn);
            return canvasGo.transform;
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
