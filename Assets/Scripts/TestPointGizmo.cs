using UnityEngine;

[ExecuteAlways] // 确保在 Editor 模式下也能运行
public class TestPointGizmo : MonoBehaviour
{
    [Header("贴纸属性")]
    public Texture2D decalTexture;
    public Color tintColor = Color.white;
    [Range(0, 1)] public float opacity = 1f;
    public DecalData.BlendMode blendMode = DecalData.BlendMode.AlphaBlend;

    [Header("可视化")]
    public bool showGizmos = true;

    private DecalData _data;
    private DecalManager _manager;

    // 1. 初始化连接
    private void OnEnable()
    {
        Initialize();
    }

    // 2. [关键修复] PlayMode 启动时强制刷新一次
    // 解决 "必须动一下才显示" 的问题
    private void Start()
    {
        Initialize();
        SyncTransformToData(); 
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
        _data.size = transform.localScale.x;
        _data.projectionDepth = transform.localScale.z;

        // 标记脏数据，通知 Manager 重绘
        _manager.MarkDirty();
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.matrix = transform.localToWorldMatrix;
        
        Gizmos.color = new Color(0, 1, 0, 0.4f);
        Gizmos.DrawWireCube(new Vector3(0, 0, 0.5f), new Vector3(1, 1, 1));
        
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(1, 1, 0.01f));

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(Vector3.zero, Vector3.forward * 1.2f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(Vector3.zero, Vector3.up * 0.5f);
    }
}