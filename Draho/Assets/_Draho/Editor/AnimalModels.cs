using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Draho.EditorTools
{
    /// <summary>
    /// Animals made in Blender (Tools/Blender/build_animals.py) in the house style: faceted, palette colours, one
    /// material (docs/ART_STYLE.md). The builders call <see cref="Instantiate"/>; when the FBX is missing they keep
    /// their old primitive animal, so the game always builds.
    /// </summary>
    public static class AnimalModels
    {
        public const string Dir = PlaygroundBuilder.Root + "/Art/Animals";
        const string PalettePng = PlaygroundBuilder.Root + "/Art/Palette/DrahoPalette.png";

        /// <summary>The one material of every palette model: the palette texture, untinted.</summary>
        public static Material PaletteMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePng);
            var m = PlaygroundBuilder.LitMaterial("DrahoPalette", Color.white, 0.15f, tex, Vector2.one);
            return m;
        }

        /// <summary>
        /// An instance of Art/Animals/[name].fbx under parent, with the palette material, the given layer and shadows,
        /// or null when the model does not exist.
        /// </summary>
        public static Transform Instantiate(string name, Transform parent, int layer)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{name}.fbx");
            if (asset == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var mat = PaletteMaterial();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.On;
            }
            return go.transform;
        }

        /// <summary>A child anywhere below root, by name.</summary>
        public static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }
    }

    /// <summary>Import settings for the palette texture and the Blender animal models.</summary>
    class AnimalModelImport : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Art/Palette/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.filterMode = FilterMode.Point;          // each face samples one flat colour cell
            ti.mipmapEnabled = false;                  // mips would blend neighbouring cells
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.sRGBTexture = true;
            ti.wrapMode = TextureWrapMode.Clamp;
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Art/Animals/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.None; // the game scripts move the parts
            mi.bakeAxisConversion = true;  // Blender Z-up to Unity Y-up for every node, pivots included
            mi.importAnimation = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // AnimalModels assigns the palette
            mi.generateSecondaryUV = false;
            mi.isReadable = false;
        }
    }
}
