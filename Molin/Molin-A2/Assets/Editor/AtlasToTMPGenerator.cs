using UnityEngine;
using UnityEditor;
using UnityEngine.U2D;
using TMPro;
using UnityEngine.TextCore;

public class AtlasToTMPGenerator : EditorWindow
{
    public SpriteAtlas sourceAtlas;

    [MenuItem("Tools/图集转 3D 字体包 (手动稳定版)")]
    public static void ShowWindow()
    {
        GetWindow<AtlasToTMPGenerator>("稳定版转换器");
    }

    void OnGUI()
    {
        GUILayout.Label("第一步：确保 Unity 原生 Sprite Atlas 已点击 Pack Preview", EditorStyles.helpBox);
        GUILayout.Label("第二步：将那个 Sprite Atlas 拖入下方槽位", EditorStyles.helpBox);
        EditorGUILayout.Space();

        sourceAtlas = (SpriteAtlas)EditorGUILayout.ObjectField("图集文件 (Sprite Atlas)", sourceAtlas, typeof(SpriteAtlas), false);

        EditorGUILayout.Space();
        if (GUILayout.Button("生成 TMP 字体资产", GUILayout.Height(35)))
        {
            Generate();
        }
    }

    private void Generate()
    {
        if (sourceAtlas == null)
        {
            Debug.LogError("错误：未选择 Sprite Atlas。");
            return;
        }

        // 直接从原生图集中提取带有正确坐标的 Sprite 数组
        Sprite[] sprites = new Sprite[sourceAtlas.spriteCount];
        sourceAtlas.GetSprites(sprites);

        if (sprites.Length == 0)
        {
            Debug.LogError("错误：图集中没有图片。请确保你在 Sprite Atlas 中添加了贴图并点击了 Pack Preview。");
            return;
        }

        TMP_SpriteAsset spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.spriteCharacterTable.Clear();
        spriteAsset.spriteGlyphTable.Clear();

        Texture2D atlasTex = null;
        if (sprites[0] != null) atlasTex = sprites[0].texture;

        // 生成材质，防止 UI 显示紫块
        if (atlasTex != null)
        {
            spriteAsset.spriteSheet = atlasTex;
            Shader shader = Shader.Find("TextMeshPro/Sprite");
            Material mat = new Material(shader);
            mat.mainTexture = atlasTex;
            AssetDatabase.CreateAsset(mat, "Assets/TMP_AtlasMaterial.mat");
            spriteAsset.material = mat;
        }

        // 遍历并映射数据
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite s = sprites[i];
            // Unity 原生图集读取时会带有 "(Clone)" 后缀，需要清理掉
            string realName = s.name.Replace("(Clone)", "");

            TMP_SpriteGlyph glyph = new TMP_SpriteGlyph();
            glyph.index = (uint)i;
            glyph.metrics = new GlyphMetrics(s.rect.width, s.rect.height, 0, s.rect.height, s.rect.width);
            glyph.glyphRect = new GlyphRect(s.rect);
            glyph.scale = 1.0f;
            glyph.atlasIndex = 0;

            spriteAsset.spriteGlyphTable.Add(glyph);

            uint unicode = (uint)realName[0];
            TMP_SpriteCharacter character = new TMP_SpriteCharacter(unicode, glyph);
            character.name = realName;

            spriteAsset.spriteCharacterTable.Add(character);
        }

        AssetDatabase.CreateAsset(spriteAsset, "Assets/TMP_3DFontAsset.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("执行完毕。已在 Assets 目录下生成 TMP_3DFontAsset.asset 和配套材质。");
    }
}