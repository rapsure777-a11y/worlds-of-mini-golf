using UnityEditor;
using UnityEngine;

namespace Gamebreak.MiniGolf.Editor.Art
{
    /// <summary>
    /// Import settings for everything the Blender pipeline writes into Assets/_Game/Art/Generated.
    /// Models: no materials (assigned by object-name suffix), Mikk tangents, readable for colliders.
    /// Textures: sRGB albedo, linear normal/height/mask, clamped alpha-preserving leaf atlas.
    /// </summary>
    public class GeneratedAssetImporter : AssetPostprocessor
    {
        const string Root = "Assets/_Game/Art/Generated/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root)) return;
            var m = (ModelImporter)assetImporter;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.animationType = ModelImporterAnimationType.None;
            m.isReadable = true;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.addCollider = false;
            m.optimizeMeshVertices = true;
            m.optimizeMeshPolygons = true;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var t = (TextureImporter)assetImporter;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            t.mipmapEnabled = true;
            t.anisoLevel = 8;
            t.wrapMode = TextureWrapMode.Repeat;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.maxTextureSize = 2048;
            if (name.EndsWith("_normal"))
            {
                t.textureType = TextureImporterType.NormalMap;
            }
            else if (name.EndsWith("_height") || name.EndsWith("_mask"))
            {
                t.textureType = TextureImporterType.Default;
                t.sRGBTexture = false;
            }
            else
            {
                t.textureType = TextureImporterType.Default;
                t.sRGBTexture = true;
            }
            if (name.StartsWith("leaves_"))
            {
                // Atlas cells must not bleed into each other; keep cut-out coverage stable in the distance.
                t.wrapMode = TextureWrapMode.Clamp;
                t.alphaIsTransparency = name.EndsWith("_albedo");
                t.mipMapsPreserveCoverage = name.EndsWith("_albedo");
                t.alphaTestReferenceValue = 0.5f;
            }
        }
    }
}
