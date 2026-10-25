using UnityEditor;

namespace PrintshopTycoon.EditorTools
{
    /// <summary>
    /// Import settings for the script-generated Blender models (Tools/Blender): metres, no
    /// animation/camera/light import, materials from the FBX colours (URP Lit).
    /// Unity converts Blender's Z-up axes itself (bakeAxisConversion), so rotations stay clean.
    /// </summary>
    public class ModelImportSettings : AssetPostprocessor
    {
        const string ModelsFolder = "Assets/PrintshopTycoon/Resources/PrintshopTycoon/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelsFolder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.isReadable = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.meshCompression = ModelImporterMeshCompression.Low;
        }
    }
}
