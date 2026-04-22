using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class AutoFontBaker : EditorWindow
{
    public List<GameObject> letterModels = new List<GameObject>();

    public int resolution = 512;
    public float orthoSize = 1f;
    public string savePath = "FontSprites";

    private SerializedObject so;
    private SerializedProperty modelsProperty;

    [MenuItem("Tools/全自动 3D 字体烘焙器")]
    public static void ShowWindow()
    {
        GetWindow<AutoFontBaker>("3D 字体烘焙器");
    }

    private void OnEnable()
    {
        so = new SerializedObject(this);
        modelsProperty = so.FindProperty("letterModels");
    }

    void OnGUI()
    {
        so.Update();

        GUILayout.Label("3D 模型转 2D 贴图工具 (左右反转修复版)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(modelsProperty, new GUIContent("字母模型列表"), true);

        Rect dragRect = GUILayoutUtility.GetRect(0f, 50f, GUILayout.ExpandWidth(true));
        GUI.Box(dragRect, "\n【 批量拖拽区域 】\n将所有 3D 字母模型直接拖到这里", EditorStyles.helpBox);
        HandleDragDrop(dragRect);

        EditorGUILayout.Space();
        resolution = EditorGUILayout.IntField("分辨率 (Resolution)", resolution);
        orthoSize = EditorGUILayout.FloatField("摄像机视野 (Ortho Size)", orthoSize);
        savePath = EditorGUILayout.TextField("保存文件夹 (Save Path)", savePath);

        so.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("开始全自动批量烘焙", GUILayout.Height(35)))
        {
            BakeModelsToPNG();
        }
    }

    private void HandleDragDrop(Rect rect)
    {
        Event evt = Event.current;
        if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
        {
            if (!rect.Contains(evt.mousePosition)) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                foreach (Object draggedObject in DragAndDrop.objectReferences)
                {
                    if (draggedObject is GameObject go)
                    {
                        if (!letterModels.Contains(go)) letterModels.Add(go);
                    }
                }
                evt.Use();
            }
        }
    }

    private void BakeModelsToPNG()
    {
        if (letterModels == null || letterModels.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "列表是空的！", "确定");
            return;
        }

        string fullPath = Path.Combine(Application.dataPath, savePath);
        if (!Directory.Exists(fullPath)) Directory.CreateDirectory(fullPath);

        GameObject camGO = new GameObject("BakeCamera");
        Camera bakeCam = camGO.AddComponent<Camera>();
        bakeCam.orthographic = true;
        bakeCam.orthographicSize = orthoSize;
        bakeCam.clearFlags = CameraClearFlags.SolidColor;
        bakeCam.backgroundColor = new Color(0, 0, 0, 0);
        camGO.transform.position = new Vector3(0, 1000, -10);

        RenderTexture rt = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.ARGB32);
        bakeCam.targetTexture = rt;

        int bakedCount = 0;
        foreach (GameObject model in letterModels)
        {
            if (model == null) continue;
            GameObject instance = Instantiate(model, new Vector3(0, 1000, 0), Quaternion.identity);

            bakeCam.Render();
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);

            // 🔥 核心修复：执行左右翻转
            Texture2D flippedTex = FlipHorizontal(tex);

            byte[] bytes = flippedTex.EncodeToPNG();
            File.WriteAllBytes(Path.Combine(fullPath, model.name + ".png"), bytes);

            DestroyImmediate(instance);
            DestroyImmediate(tex);
            DestroyImmediate(flippedTex);
            bakedCount++;
        }

        RenderTexture.active = null;
        bakeCam.targetTexture = null;
        DestroyImmediate(camGO);
        rt.Release();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("成功", $"已生成 {bakedCount} 张水平翻转后的贴图！", "太棒了");
    }

    // 🔥 水平翻转像素的方法 (左右反转)
    private Texture2D FlipHorizontal(Texture2D original)
    {
        int w = original.width;
        int h = original.height;
        Texture2D flipped = new Texture2D(w, h, TextureFormat.ARGB32, false);

        for (int y = 0; y < h; y++)
        {
            // 获取这一行的所有像素
            Color[] rowPixels = original.GetPixels(0, y, w, 1);
            // 将这一行的像素数组进行反转
            System.Array.Reverse(rowPixels);
            // 将反转后的像素写回对应的行
            flipped.SetPixels(0, y, w, 1, rowPixels);
        }

        flipped.Apply();
        return flipped;
    }
}