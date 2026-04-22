using UnityEngine;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using UnityEngine.TextCore;
using System.IO;
using System.Reflection; // 🔥 新增：必须引用反射命名空间

public class TMP_SpriteAssetPacker : EditorWindow
{
    [MenuItem("Tools/一键生成 3D 字体包 (自带图集缝合)")]
    public static void CreateSpriteAsset()
    {
        Object[] selectedObjects = Selection.objects;
        List<Texture2D> sourceTextures = new List<Texture2D>();
        List<string> charNames = new List<string>();

        foreach (Object obj in selectedObjects)
        {
            if (obj is Texture2D || obj is Sprite)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                sourceTextures.Add(tex);
                charNames.Add(obj.name);
            }
        }

        if (sourceTextures.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请在 Project 窗口全选你烘焙好的字母图片！", "确定");
            return;
        }

        // 1. 生成并保存图集
        Texture2D atlas = new Texture2D(8192, 8192, TextureFormat.ARGB32, false);
        Rect[] uvRects = atlas.PackTextures(sourceTextures.ToArray(), 2, 8192);

        string atlasPath = "Assets/My3D_Atlas.png";
        File.WriteAllBytes(atlasPath, atlas.EncodeToPNG());

        // 强制 Unity 立即导入刚保存的图片，防止读取时报错
        AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceUpdate);
        Texture2D savedAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(atlasPath);

        // 2. 生成并保存材质
        Shader shader = Shader.Find("TextMeshPro/Sprite");
        Material material = new Material(shader);
        material.mainTexture = savedAtlas;
        string matPath = "Assets/My3D_Material.mat";
        AssetDatabase.CreateAsset(material, matPath);

        // 3. 创建 Sprite Asset
        TMP_SpriteAsset spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.spriteSheet = savedAtlas;
        spriteAsset.material = material;

        // 🔥 核心修复：自己建立干净的列表，绝不调用 Unity 会报空的 API
        List<TMP_SpriteGlyph> myGlyphs = new List<TMP_SpriteGlyph>();
        List<TMP_SpriteCharacter> myCharacters = new List<TMP_SpriteCharacter>();

        for (int i = 0; i < sourceTextures.Count; i++)
        {
            Rect uv = uvRects[i];
            int x = Mathf.RoundToInt(uv.x * atlas.width);
            int y = Mathf.RoundToInt(uv.y * atlas.height);
            int w = Mathf.RoundToInt(uv.width * atlas.width);
            int h = Mathf.RoundToInt(uv.height * atlas.height);

            TMP_SpriteGlyph glyph = new TMP_SpriteGlyph();
            glyph.index = (uint)i;
            glyph.metrics = new GlyphMetrics(w, h, 0, h, w);
            glyph.glyphRect = new GlyphRect(x, y, w, h);
            glyph.scale = 1.0f;
            glyph.atlasIndex = 0;

            myGlyphs.Add(glyph);

            uint unicode = (uint)charNames[i][0];
            TMP_SpriteCharacter character = new TMP_SpriteCharacter(unicode, glyph);
            character.name = charNames[i];

            myCharacters.Add(character);
        }

        // 🔥 核心修复：通过反射直接修改底层私有变量 (m_SpriteGlyphTable 和 m_SpriteCharacterTable)
        FieldInfo glyphField = typeof(TMP_SpriteAsset).GetField("m_SpriteGlyphTable", BindingFlags.NonPublic | BindingFlags.Instance);
        if (glyphField != null) glyphField.SetValue(spriteAsset, myGlyphs);

        FieldInfo charField = typeof(TMP_SpriteAsset).GetField("m_SpriteCharacterTable", BindingFlags.NonPublic | BindingFlags.Instance);
        if (charField != null) charField.SetValue(spriteAsset, myCharacters);

        // 更新 TMP 内部字典映射
        spriteAsset.UpdateLookupTables();

        // 4. 保存字体资产
        string assetPath = "Assets/My3D_FontAsset.asset";
        AssetDatabase.CreateAsset(spriteAsset, assetPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("成功", "打包彻底完成！字体包已生成，请查看 Assets 目录。", "太棒了");
    }
}