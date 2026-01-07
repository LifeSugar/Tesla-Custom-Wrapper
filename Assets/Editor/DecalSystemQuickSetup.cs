using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

/// <summary>
/// 贴纸系统快速验证工具
/// 自动创建测试场景和配置
/// </summary>
public class DecalSystemQuickSetup : EditorWindow
{
    private GameObject carRoot;
    private Texture2D positionMap;
    private Texture2D normalMap;
    private Texture2D testDecal;
    
    [MenuItem("Tools/Tesla Painto/Quick Setup Decal System")]
    public static void ShowWindow()
    {
        GetWindow<DecalSystemQuickSetup>("快速验证贴纸系统");
    }
    
    private void OnGUI()
    {
        GUILayout.Label("贴纸系统 - 快速验证设置", EditorStyles.boldLabel);
        GUILayout.Space(10);
        
        EditorGUILayout.HelpBox("此工具将自动创建测试场景，包括DecalManager和测试脚本", MessageType.Info);
        GUILayout.Space(10);
        
        // 必需资源
        GUILayout.Label("必需资源", EditorStyles.boldLabel);
        carRoot = (GameObject)EditorGUILayout.ObjectField("车辆根节点", carRoot, typeof(GameObject), true);
        positionMap = (Texture2D)EditorGUILayout.ObjectField("Position Map (EXR)", positionMap, typeof(Texture2D), false);
        normalMap = (Texture2D)EditorGUILayout.ObjectField("Normal Map (EXR)", normalMap, typeof(Texture2D), false);
        
        GUILayout.Space(10);
        
        // 可选测试贴纸
        GUILayout.Label("测试贴纸（可选）", EditorStyles.boldLabel);
        testDecal = (Texture2D)EditorGUILayout.ObjectField("测试贴纸图片", testDecal, typeof(Texture2D), false);
        
        GUILayout.Space(20);
        
        // 自动查找按钮
        if (GUILayout.Button("🔍 自动查找烘焙的贴图", GUILayout.Height(35)))
        {
            AutoFindBakedMaps();
        }
        
        GUILayout.Space(10);
        
        // 设置按钮
        GUI.enabled = carRoot != null && positionMap != null && normalMap != null;
        if (GUILayout.Button("⚡ 一键设置测试环境", GUILayout.Height(50)))
        {
            SetupTestScene();
        }
        GUI.enabled = true;
        
        GUILayout.Space(20);
        
        // 手动验证步骤
        if (GUILayout.Button("📋 显示手动验证步骤"))
        {
            ShowManualSteps();
        }
    }
    
    /// <summary>
    /// 自动查找烘焙的贴图
    /// </summary>
    private void AutoFindBakedMaps()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures/BakedMaps" });
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            
            if (path.Contains("PosMap") && path.EndsWith(".exr"))
            {
                positionMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Debug.Log($"✅ 找到Position Map: {path}");
            }
            else if (path.Contains("NormalMap") && path.EndsWith(".exr"))
            {
                normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Debug.Log($"✅ 找到Normal Map: {path}");
            }
        }
        
        if (positionMap != null && normalMap != null)
        {
            EditorUtility.DisplayDialog("成功", "已找到烘焙的Position和Normal Map！", "好的");
        }
        else
        {
            EditorUtility.DisplayDialog("提示", "未找到完整的烘焙贴图，请先使用TeslaBakingTool烘焙。", "好的");
        }
    }
    
    /// <summary>
    /// 自动设置测试场景
    /// </summary>
    private void SetupTestScene()
    {
        // 1. 创建DecalSystem GameObject
        GameObject decalSystemObj = GameObject.Find("DecalSystem");
        if (decalSystemObj == null)
        {
            decalSystemObj = new GameObject("DecalSystem");
            Undo.RegisterCreatedObjectUndo(decalSystemObj, "Create DecalSystem");
        }
        
        DecalManager manager = decalSystemObj.GetComponent<DecalManager>();
        if (manager == null)
        {
            manager = decalSystemObj.AddComponent<DecalManager>();
        }
        
        // 配置DecalManager
        manager.positionMap = positionMap;
        manager.normalMap = normalMap;
        manager.resolution = 2048;
        
        // 尝试找到车辆材质
        if (carRoot != null)
        {
            var renderer = carRoot.GetComponentInChildren<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null)
            {
                manager.targetMaterial = renderer.sharedMaterial;
                Debug.Log($"✅ 已自动分配车辆材质: {renderer.sharedMaterial.name}");
            }
        }
        
        // 2. 创建DecalTester GameObject
        GameObject testerObj = GameObject.Find("DecalTester");
        if (testerObj == null)
        {
            testerObj = new GameObject("DecalTester");
            Undo.RegisterCreatedObjectUndo(testerObj, "Create DecalTester");
        }
        
        DecalSystemTest tester = testerObj.GetComponent<DecalSystemTest>();
        if (tester == null)
        {
            tester = testerObj.AddComponent<DecalSystemTest>();
        }
        
        // 配置Tester
        tester.decalManager = manager;
        if (testDecal != null)
        {
            tester.testDecalTexture1 = testDecal;
        }
        
        // 3. 创建测试点标记
        CreateTestPoint("TestPoint1", new Vector3(0, 1, 0), tester);
        CreateTestPoint("TestPoint2", new Vector3(0.5f, 1, 0.5f), tester);
        CreateTestPoint("TestPoint3", new Vector3(-0.5f, 1, 0.5f), tester);
        
        // 4. 选中DecalSystem对象
        Selection.activeGameObject = decalSystemObj;
        EditorGUIUtility.PingObject(decalSystemObj);
        
        // 保存场景
        EditorUtility.SetDirty(decalSystemObj);
        EditorUtility.SetDirty(testerObj);
        
        Debug.Log("<color=#00FF00>✅ 测试环境设置完成！</color>");
        
        // 显示下一步操作
        ShowNextSteps();
    }
    
    private void CreateTestPoint(string name, Vector3 position, DecalSystemTest tester)
    {
        GameObject point = GameObject.Find(name);
        if (point == null)
        {
            point = new GameObject(name);
            point.transform.position = position;
            Undo.RegisterCreatedObjectUndo(point, $"Create {name}");
            
            // 添加可视化标记
            var gizmo = point.AddComponent<TestPointGizmo>();
        }
        
        // 关联到tester
        if (name == "TestPoint1") tester.testPoint1 = point.transform;
        else if (name == "TestPoint2") tester.testPoint2 = point.transform;
        else if (name == "TestPoint3") tester.testPoint3 = point.transform;
    }
    
    private void ShowNextSteps()
    {
        string message = 
            "✅ 测试环境已就绪！\n\n" +
            "下一步操作：\n" +
            "1. 点击 Play 运行游戏\n" +
            "2. 按数字键 1/2/3 添加测试贴纸\n" +
            "3. 按 R 键添加随机贴纸\n" +
            "4. 按 C 键清空所有贴纸\n" +
            "5. 点击鼠标左键在3D位置添加贴纸\n\n" +
            "注意：如果看不到贴纸，需要修改车辆Shader添加贴纸层支持\n" +
            "（详见 DecalSystem_README.md 第3步）";
        
        EditorUtility.DisplayDialog("设置完成", message, "开始测试");
    }
    
    private void ShowManualSteps()
    {
        string steps = 
            "=== 手动验证步骤 ===\n\n" +
            "【步骤1】烘焙数据图\n" +
            "- Tools → Tesla Painto → Baker\n" +
            "- 选择 Bake Type: Both\n" +
            "- 点击烘焙按钮\n\n" +
            
            "【步骤2】创建DecalSystem\n" +
            "- 场景中创建空GameObject\n" +
            "- 添加 DecalManager 组件\n" +
            "- 拖入Position/Normal Map\n\n" +
            
            "【步骤3】添加测试脚本\n" +
            "- 创建空GameObject\n" +
            "- 添加 DecalSystemTest 组件\n" +
            "- 关联DecalManager\n" +
            "- 拖入测试贴纸图片\n\n" +
            
            "【步骤4】运行测试\n" +
            "- 点击Play\n" +
            "- 按1/2/3/R键测试\n\n" +
            
            "【步骤5】修改Shader（必需）\n" +
            "- 在车辆Shader中添加贴纸层支持\n" +
            "- 详见 DecalSystem_README.md";
        
        Debug.Log(steps);
        EditorUtility.DisplayDialog("手动验证步骤", "已在Console中输出详细步骤", "知道了");
    }
}

/// <summary>
/// 测试点可视化
/// </summary>
public class TestPointGizmo : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * 0.2f);
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.15f, gameObject.name);
        #endif
    }
}
