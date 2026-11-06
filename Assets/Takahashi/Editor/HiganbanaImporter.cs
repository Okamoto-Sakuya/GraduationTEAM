using UnityEditor;

/// <summary>
/// Assets/Resources/Higanbana 以下の画像を、取り込み時に自動で Sprite 設定にする。
/// 使う画像は 6 枚（土台・手足 4 枚・花のシート）。
/// ※ このスクリプトを入れる前に画像を取り込んだ場合は、Higanbana フォルダを右クリック → Reimport。
/// </summary>
public class HiganbanaImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Higanbana/")) return;

        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.maxTextureSize = 2048;
    }
}
