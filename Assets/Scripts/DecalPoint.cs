using UnityEngine;

[ExecuteAlways] // 确保在 Editor 模式下也能运行
public class DecalPoint : MonoBehaviour
{
    [Header("贴纸属性")]
    public Texture2D decalTexture;
    public Color tintColor = Color.white;
    [Range(0, 1)] public float opacity = 1f;
    public DecalData.BlendMode blendMode = DecalData.BlendMode.AlphaBlend;
    
    [Header("贴纸尺寸")]
    public float decalWidth = 0.2f;
    public float decalHeight = 0.2f;
    public float projectionDepth = 0.1f;

    [Header("可视化")]
    public bool showGizmos = true;
    public bool enableRuntimeGizmos = true; // PlayMode中显示交互Gizmos
    public bool useInteractiveGizmo = true; // 使用可交互的实体Gizmo

    private DecalData _data;
    private DecalManager _manager;

    // 1. 初始化连接
    private void OnEnable()
    {
        Initialize();
        // 立即尝试同步一次
        if (_manager != null && _data != null)
        {
            SyncTransformToData();
        }
    }

    // 2. [关键修复] PlayMode 启动时强制刷新一次
    // 解决 "必须动一下才显示" 的问题
    private void Start()
    {
        Initialize();
        SyncTransformToData();
        // 额外延迟刷新，确保Manager完全就绪
        StartCoroutine(StartupRefresh());
    }
    
    private System.Collections.IEnumerator StartupRefresh()
    {
        // 多次尝试刷新，确保显示
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            if (_manager != null)
            {
                SyncTransformToData();
            }
            else
            {
                Initialize();
                if (_manager != null)
                {
                    SyncTransformToData();
                }
            }
        }
    }

    // 3. 断开连接
    private void OnDisable()
    {
        if (_manager != null && _data != null)
        {
            _manager.UnregisterDecal(_data);
        }
    }

    // 4. [关键修复] Inspector 数值改变时立即刷新
    // 解决调整参数（如颜色、大小）不实时更新的问题
    private void OnValidate()
    {
        // 只有当对象被激活且已经初始化过才同步
        if (isActiveAndEnabled && _data != null)
        {
            SyncTransformToData();
        }
    }

    private void Update()
    {
        // 如果 Manager 丢失（比如重新编译脚本后），尝试重新查找
        if (_manager == null)
        {
            Initialize();
            if (_manager == null) return; // 还没找到，跳过
        }

        // 检查位置移动
        if (transform.hasChanged)
        {
            SyncTransformToData();
            transform.hasChanged = false; // 重置标记
        }
    }

    private void Initialize()
    {
        if (_data == null) _data = new DecalData { decalName = gameObject.name };

        if (_manager == null)
        {
            _manager = DecalManager.Instance;
            // 容错：如果是 Editor 模式且单例还没准备好，尝试手动查找
            if (_manager == null) _manager = FindObjectOfType<DecalManager>();
        }

        if (_manager != null)
        {
            _manager.RegisterDecal(_data);
            // 注册成功后立即同步数据
            SyncTransformToData();
        }
    }

    private void SyncTransformToData()
    {
        if (_data == null || _manager == null) return;

        // 基础属性
        _data.decalTexture = decalTexture;
        _data.tintColor = tintColor;
        _data.opacity = opacity;
        _data.blendMode = blendMode;

        // 变换属性
        _data.worldPosition = transform.position;
        _data.projectionDirection = transform.forward;
        _data.upVector = transform.up; // 保持之前的 LookRotation 修复
        _data.size = new Vector2(decalWidth, decalHeight); // 使用独立的尺寸参数
        _data.projectionDepth = projectionDepth;

        // 标记脏数据，通知 Manager 重绘
        _manager.MarkDirty();
    }

    /// <summary>
    /// 公开方法：强制刷新贴纸显示（供外部组件调用）
    /// </summary>
    public void ForceRefresh()
    {
        if (_manager == null)
        {
            Initialize();
        }
        SyncTransformToData();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        
        // 如果使用可交互Gizmo，则不绘制传统Gizmos
        if (useInteractiveGizmo)
            return;
        
        // 在PlayMode中，如果启用了运行时Gizmos，则由RuntimeGizmoController处理
        if (Application.isPlaying && enableRuntimeGizmos)
            return;
        
        // 使用自定义矩阵：位置+旋转，但缩放使用贴纸尺寸参数
        Vector3 scale = new Vector3(decalWidth, decalHeight, projectionDepth);
        Matrix4x4 gizmoMatrix = Matrix4x4.TRS(transform.position, transform.rotation, scale);
        Gizmos.matrix = gizmoMatrix;
        
        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(new Vector3(0, 0, 0.5f), new Vector3(1, 1, 1));
        
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(1, 1, 0.01f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(Vector3.zero, Vector3.forward * 1.2f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(Vector3.zero, Vector3.up * 0.5f);
    }

    [ContextMenu("添加可交互Gizmo")]
    private void AddInteractiveGizmo()
    {
        if (GetComponent<InteractiveGizmo>() == null)
        {
            InteractiveGizmo gizmo = gameObject.AddComponent<InteractiveGizmo>();
            // 应用DecalManager的默认配置
            if (DecalManager.Instance != null)
            {
                gizmo.ApplyColorConfig(DecalManager.Instance.gizmoColorConfig);
                Debug.Log($"已为 {gameObject.name} 添加 InteractiveGizmo 组件并应用默认配置");
            }
            else
            {
                Debug.Log($"已为 {gameObject.name} 添加 InteractiveGizmo 组件（未找到DecalManager，使用默认配置）");
            }
        }
        else
        {
            Debug.Log($"{gameObject.name} 已经有 InteractiveGizmo 组件了");
        }
    }

    [ContextMenu("批量添加可交互Gizmo到所有TestPoint")]
    private void AddInteractiveGizmoToAll()
    {
        DecalPoint[] allTestPoints = FindObjectsOfType<DecalPoint>();
        int addedCount = 0;
        
        foreach (DecalPoint testPoint in allTestPoints)
        {
            if (testPoint.GetComponent<InteractiveGizmo>() == null)
            {
                InteractiveGizmo gizmo = testPoint.gameObject.AddComponent<InteractiveGizmo>();
                // 应用DecalManager的默认配置
                if (DecalManager.Instance != null)
                {
                    gizmo.ApplyColorConfig(DecalManager.Instance.gizmoColorConfig);
                }
                addedCount++;
            }
        }
        
        Debug.Log($"批量操作完成：为 {addedCount} 个 TestPoint 添加了 InteractiveGizmo 组件（共找到 {allTestPoints.Length} 个）");
    }

    [ContextMenu("移除可交互Gizmo")]
    private void RemoveInteractiveGizmo()
    {
        InteractiveGizmo gizmo = GetComponent<InteractiveGizmo>();
        if (gizmo != null)
        {
            DestroyImmediate(gizmo);
            Debug.Log($"已从 {gameObject.name} 移除 InteractiveGizmo 组件");
        }
        else
        {
            Debug.Log($"{gameObject.name} 没有 InteractiveGizmo 组件");
        }
    }
}