using UnityEditor;

namespace Silkroad.EditorTools
{
    /// <summary>
    /// Assets/_Project/Art/ 아래에 넣은 이미지는 자동으로 UI 스프라이트(투명 유지, 고화질)로 가져옵니다.
    /// → 이미지를 넣기만 하면 Image 의 Source Image 칸에 바로 끌어다 놓을 수 있음.
    /// </summary>
    public class SilkroadArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/")) return;
            var imp = (TextureImporter)assetImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = false;
            imp.maxTextureSize = 4096;
            imp.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        [MenuItem("Silkroad/아트 이미지 다시 가져오기", priority = 12)]
        static void ReimportArt()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Project/Art" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
            EditorUtility.DisplayDialog("Silkroad", "Art 폴더 이미지를 스프라이트로 다시 가져왔어요.", "확인");
        }
    }
}
