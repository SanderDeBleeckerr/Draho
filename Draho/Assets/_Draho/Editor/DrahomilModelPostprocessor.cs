using UnityEditor;

namespace Draho.EditorTools
{
    /// <summary>
    /// Import settings for the cat FBX made by Tools/Blender/build_drahomil.py:
    /// generic rig, no root motion, and looping clips named Idle / Walk / Run.
    /// </summary>
    public class DrahomilModelPostprocessor : AssetPostprocessor
    {
        static bool IsCat(string path) => path.EndsWith("/Drahomil.fbx");

        void OnPreprocessModel()
        {
            if (!IsCat(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.animationType = ModelImporterAnimationType.Generic;
        }

        void OnPreprocessAnimation()
        {
            if (!IsCat(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                // Blender names takes "DrahomilRig|Walk"; keep the last part.
                int bar = clip.takeName.LastIndexOf('|');
                clip.name = bar >= 0 ? clip.takeName.Substring(bar + 1) : clip.takeName;
                clip.loopTime = true;
            }
            importer.clipAnimations = clips;
        }
    }
}
