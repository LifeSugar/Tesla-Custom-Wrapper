using UnityEngine;

/// <summary>
/// 贴纸系统测试脚本 - 用于快速测试贴纸功能
/// 使用方法：
/// 1. 将此脚本挂载到场景中的GameObject上
/// 2. 在Inspector中拖入DecalManager引用
/// 3. 运行游戏，按数字键测试不同功能
/// </summary>
public class DecalSystemTest : MonoBehaviour
{
    [Header("测试设置")]
    public DecalManager decalManager;
    
    [Header("测试贴纸素材")]
    public Texture2D testDecalTexture1;
    public Texture2D testDecalTexture2;
    public Texture2D testDecalTexture3;
    
    [Header("测试位置")]
    public Transform testPoint1; // 可选：在场景中标记测试点
    public Transform testPoint2;
    public Transform testPoint3;
    
    private void Update()
    {
        if (decalManager == null)
        {
            Debug.LogWarning("⚠️ 请先拖入DecalManager引用！");
            return;
        }
        
        // 按键1: 添加第一个测试贴纸
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            AddTestDecal(testDecalTexture1, GetTestPosition(testPoint1, new Vector3(0, 1, 0)), Vector3.down);
        }
        
        // 按键2: 添加第二个测试贴纸
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            AddTestDecal(testDecalTexture2, GetTestPosition(testPoint2, new Vector3(0.5f, 1, 0.5f)), Vector3.down);
        }
        
        // 按键3: 添加第三个测试贴纸
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            AddTestDecal(testDecalTexture3, GetTestPosition(testPoint3, new Vector3(-0.5f, 1, 0.5f)), Vector3.down);
        }
        
        // 按键C: 清空所有贴纸
        if (Input.GetKeyDown(KeyCode.C))
        {
            Debug.Log("🗑️ 清空所有贴纸");
            decalManager.ClearAllDecals();
        }
        
        // 按键R: 随机生成贴纸
        if (Input.GetKeyDown(KeyCode.R))
        {
            AddRandomDecal();
        }
        
        // 按键Space: 在鼠标点击位置添加贴纸（需要Raycast）
        if (Input.GetMouseButtonDown(0))
        {
            TryAddDecalAtMousePosition();
        }
    }
    
    /// <summary>
    /// 获取测试位置（优先使用Transform，否则使用默认值）
    /// </summary>
    private Vector3 GetTestPosition(Transform point, Vector3 defaultPos)
    {
        return point != null ? point.position : defaultPos;
    }
    
    /// <summary>
    /// 添加测试贴纸
    /// </summary>
    private void AddTestDecal(Texture2D texture, Vector3 position, Vector3 direction)
    {
        if (texture == null)
        {
            Debug.LogWarning("⚠️ 测试贴纸纹理未设置！");
            return;
        }
        
        DecalData decal = new DecalData
        {
            decalName = $"Test Decal {decalManager.decals.Count + 1}",
            decalTexture = texture,
            worldPosition = position,
            projectionDirection = direction.normalized,
            size = 0.2f,
            rotation = Random.Range(0f, 360f),
            opacity = 1f,
            tintColor = Color.white,
            blendMode = DecalData.BlendMode.AlphaBlend
        };
        
        decalManager.AddDecal(decal);
        Debug.Log($"✅ 添加贴纸: {decal.decalName} at {position}");
    }
    
    /// <summary>
    /// 添加随机贴纸
    /// </summary>
    private void AddRandomDecal()
    {
        // 随机选择一个贴纸纹理
        Texture2D[] textures = { testDecalTexture1, testDecalTexture2, testDecalTexture3 };
        Texture2D randomTexture = textures[Random.Range(0, textures.Length)];
        
        if (randomTexture == null)
        {
            Debug.LogWarning("⚠️ 没有可用的测试贴纸纹理！");
            return;
        }
        
        // 随机位置和参数
        Vector3 randomPos = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0.5f, 1.5f),
            Random.Range(-1f, 1f)
        );
        
        DecalData decal = new DecalData
        {
            decalName = $"Random Decal {decalManager.decals.Count + 1}",
            decalTexture = randomTexture,
            worldPosition = randomPos,
            projectionDirection = Random.insideUnitSphere.normalized,
            size = Random.Range(0.1f, 0.3f),
            rotation = Random.Range(0f, 360f),
            opacity = Random.Range(0.7f, 1f),
            tintColor = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.8f, 1f),
            blendMode = (DecalData.BlendMode)Random.Range(0, 3)
        };
        
        decalManager.AddDecal(decal);
        Debug.Log($"✅ 添加随机贴纸: {decal.decalName}");
    }
    
    /// <summary>
    /// 尝试在鼠标点击位置添加贴纸（使用Raycast）
    /// </summary>
    private void TryAddDecalAtMousePosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit))
        {
            // 使用第一个贴纸纹理
            Texture2D texture = testDecalTexture1 != null ? testDecalTexture1 : 
                               testDecalTexture2 != null ? testDecalTexture2 : 
                               testDecalTexture3;
            
            if (texture == null)
            {
                Debug.LogWarning("⚠️ 没有可用的测试贴纸纹理！");
                return;
            }
            
            DecalData decal = new DecalData
            {
                decalName = $"Mouse Decal {decalManager.decals.Count + 1}",
                decalTexture = texture,
                worldPosition = hit.point,
                projectionDirection = -hit.normal, // 沿着表面法线投影
                size = 0.15f,
                rotation = 0f,
                opacity = 1f,
                tintColor = Color.white,
                blendMode = DecalData.BlendMode.AlphaBlend
            };
            
            decalManager.AddDecal(decal);
            Debug.Log($"✅ 在鼠标位置添加贴纸: {hit.point}");
        }
    }
    
    private void OnGUI()
    {
        // 在屏幕左上角显示帮助信息
        GUIStyle style = new GUIStyle(GUI.skin.box);
        style.fontSize = 14;
        style.alignment = TextAnchor.UpperLeft;
        style.normal.textColor = Color.white;
        
        string helpText = 
            "=== 贴纸系统测试 ===\n" +
            "1 - 添加测试贴纸1\n" +
            "2 - 添加测试贴纸2\n" +
            "3 - 添加测试贴纸3\n" +
            "R - 添加随机贴纸\n" +
            "C - 清空所有贴纸\n" +
            "鼠标左键 - 在点击位置添加贴纸\n" +
            $"\n当前贴纸数: {(decalManager != null ? decalManager.decals.Count : 0)}";
        
        GUI.Box(new Rect(10, 10, 300, 180), helpText, style);
    }
}
