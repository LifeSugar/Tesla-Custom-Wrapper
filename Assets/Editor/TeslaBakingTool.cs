using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.IO;

public class TeslaBakingTool : EditorWindow
{
    // UI 变量
    private GameObject targetCarRoot; // 这里改成 Root，表示根节点
    private int resolution = 2048; 
    private Material bakePosMat;
    private Material bakeNormalMat;
    private string savePath = "Assets/Textures/BakedMaps";
    
    // 烘焙类型枚举
    private enum BakeType
    {
        Position,
        Normal,
        Both
    }
    private BakeType bakeType = BakeType.Position;

    [MenuItem("Tools/Tesla Painto/Baker (Multi-Mesh)")]
    public static void ShowWindow()
    {
        GetWindow<TeslaBakingTool>("PosMap Baker");
    }

    private void OnEnable()
    {
        // 加载位置烘焙shader
        Shader posShader = Shader.Find("Hidden/Tesla_Bake_ObjectPos");
        if (posShader != null) 
            bakePosMat = new Material(posShader);
        else 
            Debug.LogError("❌ 找不到 Shader 'Hidden/Tesla_Bake_ObjectPos'！");
        
        // 加载法线烘焙shader
        Shader normalShader = Shader.Find("Hidden/Tesla_Bake_ObjectNormal");
        if (normalShader != null) 
            bakeNormalMat = new Material(normalShader);
        else 
            Debug.LogError("❌ 找不到 Shader 'Hidden/Tesla_Bake_ObjectNormal'！");
    }

    private void OnGUI()
    {
        GUILayout.Label("Tesla 多部件合并烘焙器", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // 1. 选择根节点
        targetCarRoot = (GameObject)EditorGUILayout.ObjectField("车辆根节点 (Parent)", targetCarRoot, typeof(GameObject), true);
        
        // 2. 分辨率
        resolution = EditorGUILayout.IntPopup("贴图分辨率", resolution, new string[] { "1024", "2048", "4096" }, new int[] { 1024, 2048, 4096 });
        
        // 3. 烘焙类型选择
        GUILayout.Space(10);
        GUILayout.Label("烘焙类型", EditorStyles.boldLabel);
        bakeType = (BakeType)EditorGUILayout.EnumPopup("Bake Type", bakeType);

        GUILayout.Space(20);

        // 4. 根据选择的类型显示按钮
        if (bakeType == BakeType.Position)
        {
            if (GUILayout.Button("烘焙位置图 (Bake Position Map)", GUILayout.Height(40)))
            {
                if (CheckRequirements()) BakePositionMap();
            }
        }
        else if (bakeType == BakeType.Normal)
        {
            if (GUILayout.Button("烘焙法线图 (Bake Normal Map)", GUILayout.Height(40)))
            {
                if (CheckRequirements()) BakeNormalMap();
            }
        }
        else // Both
        {
            if (GUILayout.Button("烘焙位置和法线图 (Bake Both Maps)", GUILayout.Height(40)))
            {
                if (CheckRequirements()) 
                {
                    BakePositionMap();
                    BakeNormalMap();
                }
            }
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox("提示：\n1. 确保所有子物体 Mesh 都共用同一个原点 (Pivot)。\n2. 确保它们的 UV 不重叠 (符合官方模板布局)。\n3. 物体空间法线图用于存储未经变换的原始法线数据。", MessageType.Info);
    }

    private bool CheckRequirements()
    {
        if (targetCarRoot == null) {
            EditorUtility.DisplayDialog("错误", "请拖入包含所有部件的父物体！", "好的");
            return false;
        }
        
        // 根据烘焙类型检查对应的材质
        if (bakeType == BakeType.Position || bakeType == BakeType.Both)
        {
            if (bakePosMat == null)
            {
                EditorUtility.DisplayDialog("错误", "位置烘焙材质未加载！", "好的");
                return false;
            }
        }
        
        if (bakeType == BakeType.Normal || bakeType == BakeType.Both)
        {
            if (bakeNormalMat == null)
            {
                EditorUtility.DisplayDialog("错误", "法线烘焙材质未加载！", "好的");
                return false;
            }
        }
        
        return true;
    }

    private void BakePositionMap()
    {
        // A. 搜集所有 MeshFilter
        MeshFilter[] meshFilters = targetCarRoot.GetComponentsInChildren<MeshFilter>();
        
        if (meshFilters.Length == 0) {
            Debug.LogError("没有在这个物体下找到任何 Mesh！");
            return;
        }

        // B. 创建画布 (RT)
        RenderTexture rt = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        
        // C. 使用 CommandBuffer 进行渲染 (URP 标准方式)
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "Bake Position Map";
        
        // 设置渲染目标并清空
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));
        
        // D. 遍历所有部件进行绘制
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            
            // 使用 CommandBuffer.DrawMesh 绘制
            // 关键：使用 identity 矩阵，因为 shader 内部会用 UV 定位
            cmd.DrawMesh(mf.sharedMesh, Matrix4x4.identity, bakePosMat, 0, 0);
        }
        
        // 执行 CommandBuffer
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();

        // E. 读取结果
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);
        tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        SaveTextureAsEXR(tex, "PosMap");
        DestroyImmediate(tex);
    }
    
    private void BakeNormalMap()
    {
        // A. 搜集所有 MeshFilter
        MeshFilter[] meshFilters = targetCarRoot.GetComponentsInChildren<MeshFilter>();
        
        if (meshFilters.Length == 0) {
            Debug.LogError("没有在这个物体下找到任何 Mesh！");
            return;
        }

        // B. 创建画布 (RT)
        RenderTexture rt = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGBFloat);
        
        // C. 使用 CommandBuffer 进行渲染 (URP 标准方式)
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "Bake Normal Map";
        
        // 设置渲染目标并清空
        cmd.SetRenderTarget(rt);
        cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));
        
        // D. 遍历所有部件进行绘制
        foreach (var mf in meshFilters)
        {
            if (mf.sharedMesh == null) continue;
            
            // 使用 CommandBuffer.DrawMesh 绘制法线
            cmd.DrawMesh(mf.sharedMesh, Matrix4x4.identity, bakeNormalMat, 0, 0);
        }
        
        // 执行 CommandBuffer
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();

        // E. 读取结果
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false);
        tex.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
        tex.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        SaveTextureAsEXR(tex, "NormalMap");
        DestroyImmediate(tex);
    }

    private void SaveTextureAsEXR(Texture2D tex, string mapType)
    {
        if (!Directory.Exists(savePath)) Directory.CreateDirectory(savePath);
        string fileName = $"{targetCarRoot.name}_Merged_{mapType}.exr";
        string fullPath = Path.Combine(savePath, fileName);
        File.WriteAllBytes(fullPath, tex.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat));
        AssetDatabase.Refresh();
        
        // 自动设置 Import Settings
        ApplyImportSettings(fullPath);
        
        Debug.Log($"<color=#00FF00>✅ {mapType} 合并烘焙成功！包含 {targetCarRoot.GetComponentsInChildren<MeshFilter>().Length} 个部件。路径: {fullPath}</color>");
    }

    private void ApplyImportSettings(string path)
    {
        // 自动帮你把 sRGB 关掉，省得你忘了
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null) {
            importer.sRGBTexture = false; // 必须关掉！
            importer.textureCompression = TextureImporterCompression.Uncompressed; // 保持精度
            importer.mipmapEnabled = false; 
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}