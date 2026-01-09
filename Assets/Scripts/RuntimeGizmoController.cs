using UnityEngine;
using System;

/// <summary>
/// 运行时移动轴控制器
/// 核心功能：屏幕恒定大小、射线-平面求交拖拽、事件驱动架构
/// </summary>
public class RuntimeGizmoController : MonoBehaviour
{
    public static RuntimeGizmoController Instance { get; private set; }
    
    public enum GizmoType
    {
        Translation,
        Rotation,
        Scale
    }

    public enum GizmoSpace
    {
        Global,
        Local
    }

    #region 配置参数

    [Header("模式配置")]
    [Tooltip("Gizmo 操作模式")]
    [SerializeField] private GizmoType gizmoType = GizmoType.Translation;

    [Tooltip("Gizmo 坐标系模式")]
    [SerializeField] private GizmoSpace gizmoSpace = GizmoSpace.Global;

    [Header("基础配置")]
    [Tooltip("目标物体（要操作的对象）")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("渲染相机")]
    [SerializeField] private Camera renderCamera;

    [Tooltip("Gizmo 专用 Layer（建议设置为独立 Layer 避免干扰）")]
    [SerializeField] private int gizmoLayer = 31;

    [Header("视觉配置")]
    [Tooltip("屏幕恒定大小系数（调整此值改变 Gizmo 视觉大小）")]
    [SerializeField] private float screenSizeCoefficient = 0.1f;

    [Tooltip("轴杆长度")]
    [SerializeField] private float axisLength = 1f;

    [Tooltip("轴杆半径")]
    [SerializeField] private float axisRadius = 0.02f;

    [Tooltip("箭头高度")]
    [SerializeField] private float arrowHeight = 0.2f;

    [Tooltip("箭头半径")]
    [SerializeField] private float arrowRadius = 0.06f;

    [Tooltip("平面方块大小")]
    [SerializeField] private float planeSize = 0.3f;

    [Tooltip("平面方块偏移")]
    [SerializeField] private float planeOffset = 0f;

    [Tooltip("平面方块透明度")]
    [SerializeField] [Range(0, 1)] private float planeAlpha = 0.2f;

    [Header("旋转配置")]
    [Tooltip("旋转圆环半径")]
    [SerializeField] private float rotationRadius = 1f;

    [Tooltip("旋转圆环粗细")]
    [SerializeField] private float rotationThickness = 0.05f;

    [Tooltip("旋转屏幕圆环颜色")]
    [SerializeField] private Color screenAxisColor = Color.white;

    [Header("颜色配置")]
    [SerializeField] private Color xAxisColor = Color.red;
    [SerializeField] private Color yAxisColor = Color.green;
    [SerializeField] private Color zAxisColor = Color.blue;
    [SerializeField] private Color highlightColor = Color.yellow;

    [Header("Shader 配置")]
    [Tooltip("Gizmo 使用的 Shader")]
    [SerializeField] private Shader gizmoShader;

    #endregion

    #region 事件定义

    /// <summary>
    /// 开始拖拽事件
    /// </summary>
    public event Action<GizmoHandle.Axis> OnBeginDrag;

    /// <summary>
    /// 拖拽中事件（返回世界空间位移增量）
    /// </summary>
    public event Action<Vector3> OnDrag;

    /// <summary>
    /// 结束拖拽事件
    /// </summary>
    public event Action OnEndDrag;

    #endregion

    #region 私有变量

    private GameObject gizmoRoot;
    private GameObject translationRoot;
    private GameObject rotationRoot;
    
    // Fan Visuals
    private GameObject rotationFanObject;
    private Material fanMaterial;
    private Mesh rotationFanMesh;
    private Quaternion fanInitialRotation; // 扇形初始旋转

    private GizmoHandle[] translationHandles;
    private GizmoHandle[] rotationHandles;

    private Material gizmoMaterial;

    // 交互状态
    private GizmoHandle currentDragHandle;
    private bool isDragging = false;
    private Vector3 lastWorldPosition;
    private Plane dragPlane;
    
    // 旋转专用交互状态
    private float startAngle = 0f;
    private float lastAngle = 0f;
    private float currentRotationAngleAccumulator = 0f;
    private Vector3 rotationAxis; // 旋转轴（用于 Apply Rotation）

    // 悬停状态
    private GizmoHandle hoveredHandle;

    // 射线检测
    private LayerMask gizmoLayerMask;

    #endregion

    #region Unity 生命周期

    void Awake()
    {
        // 单例设置
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // 自动查找相机
        if (renderCamera == null)
        {
            renderCamera = Camera.main;
            if (renderCamera == null)
            {
                Debug.LogError("[RuntimeGizmoController] 未找到渲染相机！");
                enabled = false;
                return;
            }
        }

        // 加载 Shader
        if (gizmoShader == null)
        {
            gizmoShader = Shader.Find("Hidden/RuntimeGizmo");
            if (gizmoShader == null)
            {
                Debug.LogError("[RuntimeGizmoController] 未找到 RuntimeGizmo Shader！");
                enabled = false;
                return;
            }
        }

        // 初始化 LayerMask
        gizmoLayerMask = 1 << gizmoLayer;

        // 创建 Gizmo
        CreateGizmo();
    }

    void LateUpdate()
    {
        if (targetTransform == null || gizmoRoot == null)
            return;

        // 1. 先处理输入（因为拖拽会改变目标位置）
        HandleInput();

        // 2. 更新 Gizmo 位置以匹配目标（消除拖拽时的视觉延迟）
        gizmoRoot.transform.position = targetTransform.position;

        // 更新 Gizmo 旋转以匹配坐标系模式
        if (gizmoSpace == GizmoSpace.Local)
        {
            gizmoRoot.transform.rotation = targetTransform.rotation;
        }
        else
        {
            gizmoRoot.transform.rotation = Quaternion.identity;
        }

        // 3. 屏幕恒定大小
        UpdateScreenConstantSize();

        // 4. 处理旋转 Gizmo 的屏幕圆环朝向 (始终面向相机)
        // [New Feature] Screen Ring 已移除，该逻辑不再需要
    }

    /* 
    private void UpdateRotationScreenRing()
    {
        // ... Removed
    } 
    */

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        
        if (gizmoRoot != null)
        {
            Destroy(gizmoRoot);
        }

        if (gizmoMaterial != null)
        {
            Destroy(gizmoMaterial);
        }
    }

    #endregion

    #region 公共 API

    /// <summary>
    /// 设置目标物体
    /// </summary>
    public void SetTarget(Transform target)
    {
        targetTransform = target;
        if (gizmoRoot != null && target != null)
        {
            gizmoRoot.transform.position = target.position;
        }
    }

    /// <summary>
    /// 设置渲染相机
    /// </summary>
    public void SetCamera(Camera cam)
    {
        renderCamera = cam;
    }

    /// <summary>
    /// 设置 Gizmo 模式
    /// </summary>
    public void SetGizmoType(GizmoType type)
    {
        if (gizmoType == type) return;
        gizmoType = type;
        
        // 切换显示状态
        if (translationRoot != null) translationRoot.SetActive(gizmoType == GizmoType.Translation);
        if (rotationRoot != null) rotationRoot.SetActive(gizmoType == GizmoType.Rotation);
        // Scale模式下隐藏所有Gizmo
        
        // 结束当前可能的拖拽
        if (isDragging) EndDrag();
    }

    /// <summary>
    /// 设置坐标系模式
    /// </summary>
    public void SetGizmoSpace(GizmoSpace space)
    {
        if (gizmoSpace == space) return;
        gizmoSpace = space;
        
        // 结束当前可能的拖拽，因为坐标系变化会导致轴向突变
        if (isDragging) EndDrag();
    }

    /// <summary>
    /// 是否正在拖拽
    /// </summary>
    public bool IsDragging()
    {
        return isDragging;
    }

    #endregion

    #region Gizmo 创建

    private void CreateGizmo()
    {
        // 清理旧 Gizmo
        if (gizmoRoot != null)
        {
            Destroy(gizmoRoot);
        }

        // 创建主根节点
        gizmoRoot = new GameObject("RuntimeGizmo");
        gizmoRoot.transform.position = targetTransform != null ? targetTransform.position : Vector3.zero;
        gizmoRoot.transform.SetParent(transform);

        // 创建材质
        if (gizmoMaterial == null) 
            gizmoMaterial = new Material(gizmoShader);

        // --- 初始化由 Translation Root ---
        translationRoot = new GameObject("TranslationGizmo");
        translationRoot.transform.SetParent(gizmoRoot.transform, false);
        CreateTranslationGizmo();

        // --- 初始化 Rotation Root ---
        rotationRoot = new GameObject("RotationGizmo");
        rotationRoot.transform.SetParent(gizmoRoot.transform, false);
        CreateRotationGizmo();
        
        // 根据初始模式设置显隐
        translationRoot.SetActive(gizmoType == GizmoType.Translation);
        rotationRoot.SetActive(gizmoType == GizmoType.Rotation);
        // Scale模式下隐藏所有Gizmo

        Debug.Log($"[RuntimeGizmoController] Gizmo 初始化完成，当前模式={gizmoType}");
    }

    private void CreateTranslationGizmo()
    {
        // 创建6个Handle：3个轴 + 3个平面
        translationHandles = new GizmoHandle[6]; 
        
        // 3个轴向
        translationHandles[0] = CreateAxis(GizmoHandle.Axis.X, xAxisColor, Vector3.right, translationRoot.transform);
        translationHandles[1] = CreateAxis(GizmoHandle.Axis.Y, yAxisColor, Vector3.up, translationRoot.transform);
        translationHandles[2] = CreateAxis(GizmoHandle.Axis.Z, zAxisColor, Vector3.forward, translationRoot.transform);
        
        // 3个平面
        Color xyColor = zAxisColor; xyColor.a = planeAlpha;
        Color xzColor = yAxisColor; xzColor.a = planeAlpha;
        Color yzColor = xAxisColor; yzColor.a = planeAlpha;

        translationHandles[3] = CreatePlane(GizmoHandle.Axis.XY, xyColor, translationRoot.transform);
        translationHandles[4] = CreatePlane(GizmoHandle.Axis.XZ, xzColor, translationRoot.transform);
        translationHandles[5] = CreatePlane(GizmoHandle.Axis.YZ, yzColor, translationRoot.transform);
    }

    private void CreateRotationGizmo()
    {
        // 3个轴向，不再需要 Screen Axis Handles[3]
        rotationHandles = new GizmoHandle[3];

        // X: XZ to YZ plane (Normal X)
        rotationHandles[0] = CreateRotationRing(GizmoHandle.Axis.X, xAxisColor, rotationRoot.transform); 
        
        // Y: XZ plane (Normal Y)
        rotationHandles[1] = CreateRotationRing(GizmoHandle.Axis.Y, yAxisColor, rotationRoot.transform);

        // Z: XZ to XY plane (Normal Z)
        rotationHandles[2] = CreateRotationRing(GizmoHandle.Axis.Z, zAxisColor, rotationRoot.transform);

        // 创建扇形显示对象 (需确保 Shader 存在)
        CreateRotationFan();
    }

    private void CreateRotationFan()
    {
        if (rotationFanObject != null) Destroy(rotationFanObject);

        rotationFanObject = new GameObject("RotationFan");
        rotationFanObject.transform.SetParent(rotationRoot.transform, false);
        rotationFanObject.layer = gizmoLayer;
        
        // 扇形默认隐藏
        rotationFanObject.SetActive(false);

        // 使用 Quad 网格
        MeshFilter mf = rotationFanObject.AddComponent<MeshFilter>();
        
        // 我们需要一个中心对齐的 Quad，大小为 rotationRadius * 2
        // 手动创建一个中心对齐的 Quad Mesh
        if (rotationFanMesh == null)
        {
            rotationFanMesh = new Mesh();
            rotationFanMesh.name = "FanQuad";
            float size = rotationRadius * 2f; // 直径
            float half = size * 0.5f;
            rotationFanMesh.vertices = new Vector3[] { 
                new Vector3(-half, -half, 0), new Vector3(half, -half, 0), 
                new Vector3(-half, half, 0), new Vector3(half, half, 0) 
            };
            rotationFanMesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
            rotationFanMesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        }
        mf.mesh = rotationFanMesh;

        MeshRenderer mr = rotationFanObject.AddComponent<MeshRenderer>();
        // 加载 Fan Shader
        Shader fanShader = Shader.Find("Hidden/RuntimeGizmoFan");
        if (fanShader == null)
        {
            Debug.LogWarning("RuntimeGizmoFan shader not found! Checking fallback...");
            // 尝试直接加载刚才创建的文件路径 (仅在 Editor下有效，Runtime 需 ensure included)
             fanShader = Shader.Find("Hidden/RuntimeGizmoFan");
        }
        
        if (fanShader != null)
        {
            fanMaterial = new Material(fanShader);
            fanMaterial.SetColor("_Color", new Color(1, 1, 0, 0.4f)); // 黄色半透明
            mr.material = fanMaterial;
        }
        else 
        {
            Debug.LogError("RuntimeGizmoFan shader is missing. Please ensure it is in a Resources folder or included in build.");
        }
    }


    // 重构：这里需要把 transform parent 改为参数传入
    private GizmoHandle CreateAxis(GizmoHandle.Axis axis, Color color, Vector3 direction, Transform parent)
    {
        GameObject axisObject = new GameObject($"Axis_{axis}");
        axisObject.transform.SetParent(parent, false);
        axisObject.layer = gizmoLayer;
        // ... (后续复用旧逻辑，只需要确保 parent 参数正确)
        
        // 复制之前的 CreateAxis 逻辑，只改 parent
        // ...
        
        // 为了避免全量粘贴，我将使用新的辅助函数，或者直接在这里完整重写这个小函数
        // 这里我选择直接重写 CreateAxis 以适配 parent
        
        // 创建轴杆
        GameObject shaft = new GameObject("Shaft");
        shaft.transform.SetParent(axisObject.transform, false);
        shaft.transform.localPosition = Vector3.zero;
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        shaft.layer = gizmoLayer;

        MeshFilter shaftMF = shaft.AddComponent<MeshFilter>();
        shaftMF.mesh = GizmoMeshFactory.CreateCylinder(axisRadius, axisLength);
        MeshRenderer shaftMR = shaft.AddComponent<MeshRenderer>();
        shaftMR.material = gizmoMaterial;

        // 创建箭头
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.SetParent(axisObject.transform, false);
        arrow.transform.localPosition = direction * axisLength;
        arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        arrow.layer = gizmoLayer;

        MeshFilter arrowMF = arrow.AddComponent<MeshFilter>();
        arrowMF.mesh = GizmoMeshFactory.CreateCone(arrowRadius, arrowHeight);
        MeshRenderer arrowMR = arrow.AddComponent<MeshRenderer>();
        arrowMR.material = gizmoMaterial;

        // 碰撞体
        BoxCollider collider = axisObject.AddComponent<BoxCollider>();
        collider.center = direction * (axisLength * 0.5f);
        collider.size = new Vector3(
            direction.x != 0 ? axisLength + arrowHeight : arrowRadius * 2,
            direction.y != 0 ? axisLength + arrowHeight : arrowRadius * 2,
            direction.z != 0 ? axisLength + arrowHeight : arrowRadius * 2
        );

        GizmoHandle handle = axisObject.AddComponent<GizmoHandle>();
        handle.axis = axis;
        handle.normalColor = color;
        handle.highlightColor = highlightColor;
        handle.SetColor(color);

        return handle;
    }

    private GizmoHandle CreatePlane(GizmoHandle.Axis axis, Color color, Transform parent)
    {
        string name = $"Plane_{axis}";
        GameObject planeObj = new GameObject(name);
        planeObj.transform.SetParent(parent, false);
        planeObj.layer = gizmoLayer;

        Mesh mesh = GizmoMeshFactory.CreateQuad(planeSize, planeOffset);
        
        if (axis == GizmoHandle.Axis.XZ)
            planeObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
        else if (axis == GizmoHandle.Axis.YZ)
            planeObj.transform.localRotation = Quaternion.Euler(0, -90, 0);
        else 
            planeObj.transform.localRotation = Quaternion.identity;

        MeshFilter mf = planeObj.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = planeObj.AddComponent<MeshRenderer>();
        mr.material = gizmoMaterial;

        MeshCollider collider = planeObj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        GizmoHandle handle = planeObj.AddComponent<GizmoHandle>();
        handle.axis = axis;
        handle.normalColor = color;
        handle.highlightColor = highlightColor;
        handle.SetColor(color);

        return handle;
    }

    private GizmoHandle CreateRotationRing(GizmoHandle.Axis axis, Color color, Transform parent)
    {
        GameObject ringObj = new GameObject($"Ring_{axis}");
        ringObj.transform.SetParent(parent, false);
        ringObj.layer = gizmoLayer;

        // Mesh 默认为 XZ 平面
        Mesh mesh = GizmoMeshFactory.CreateTorus(rotationRadius, rotationThickness);
        
        if (axis == GizmoHandle.Axis.X)
            ringObj.transform.localRotation = Quaternion.Euler(0, 0, 90);
        else if (axis == GizmoHandle.Axis.Y)
            ringObj.transform.localRotation = Quaternion.identity;
        else if (axis == GizmoHandle.Axis.Z)
            ringObj.transform.localRotation = Quaternion.Euler(90, 0, 0);

        MeshFilter mf = ringObj.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = ringObj.AddComponent<MeshRenderer>();
        mr.material = gizmoMaterial;

        // 使用 Mesh Collider 实现精确点击
        MeshCollider collider = ringObj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        GizmoHandle handle = ringObj.AddComponent<GizmoHandle>();
        handle.axis = axis;
        handle.normalColor = color;
        handle.highlightColor = highlightColor;
        handle.SetColor(color);

        return handle;
    }

    /// <summary>
    /// 创建平面拖拽块
    /// </summary>
    private GizmoHandle CreatePlane(GizmoHandle.Axis axis, Color color)
    {
        string name = $"Plane_{axis}";
        GameObject planeObj = new GameObject(name);
        planeObj.transform.SetParent(gizmoRoot.transform, false);
        planeObj.layer = gizmoLayer;

        // 设置旋转和网格并创建碰撞体
        // CreateQuad 默认生成在 XY 平面 (offset -> offset+size)
        // 我们需要根据轴向旋转这个 Quad
        Mesh mesh = GizmoMeshFactory.CreateQuad(planeSize, planeOffset);
        
        if (axis == GizmoHandle.Axis.XZ)
        {
            // 旋转到 XZ 平面 (绕 X 轴旋转 90 度)
            planeObj.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        else if (axis == GizmoHandle.Axis.YZ)
        {
            // 旋转到 YZ 平面 (绕 Y 轴旋转 -90 度，使得 Z 向前 X 向左? 不，我们要确保在 +Y +Z 象限)
            // 原始 Quad: X(0->size), Y(0->size), Z(0)
            // 转为 YZ: X(0), Y(0->size), Z(0->size)
            // 绕 Y 转 -90 度: X -> Z, Y -> Y
            planeObj.transform.localRotation = Quaternion.Euler(0, -90, 0);
        }
        else // XY 平面
        {
            planeObj.transform.localRotation = Quaternion.identity;
        }

        MeshFilter mf = planeObj.AddComponent<MeshFilter>();
        mf.mesh = mesh;
        MeshRenderer mr = planeObj.AddComponent<MeshRenderer>();
        mr.material = gizmoMaterial;

        // 碰撞体
        MeshCollider collider = planeObj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        GizmoHandle handle = planeObj.AddComponent<GizmoHandle>();
        handle.axis = axis;
        handle.normalColor = color;
        handle.highlightColor = highlightColor;
        handle.SetColor(color);

        return handle;
    }

    // 废弃 CreateCenterHandle
    // private GizmoHandle CreateCenterHandle(...)

    private GizmoHandle CreateAxis(GizmoHandle.Axis axis, Color color, Vector3 direction)
    {
        // 创建轴容器
        GameObject axisObject = new GameObject($"Axis_{axis}");
        axisObject.transform.SetParent(gizmoRoot.transform, false);
        axisObject.layer = gizmoLayer;

        // 创建轴杆（圆柱体）
        GameObject shaft = new GameObject("Shaft");
        shaft.transform.SetParent(axisObject.transform, false);
        // 修正：网格默认 pivot 在底部 (0,0,0)，延伸至 (0, Height, 0)
        // 旋转后延伸至 (0, 0, Lenght) * direction，起始终点无需偏移
        shaft.transform.localPosition = Vector3.zero;
        shaft.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        shaft.layer = gizmoLayer;

        MeshFilter shaftMF = shaft.AddComponent<MeshFilter>();
        shaftMF.mesh = GizmoMeshFactory.CreateCylinder(axisRadius, axisLength);
        MeshRenderer shaftMR = shaft.AddComponent<MeshRenderer>();
        shaftMR.material = gizmoMaterial;

        // 创建箭头（圆锥体）
        GameObject arrow = new GameObject("Arrow");
        arrow.transform.SetParent(axisObject.transform, false);
        // 修正：箭头应该接在轴杆末端，即 axisLength 处
        // 圆锥网格 pivot 也是底部，所以位置设为轴向终点即可
        arrow.transform.localPosition = direction * axisLength;
        arrow.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
        arrow.layer = gizmoLayer;

        MeshFilter arrowMF = arrow.AddComponent<MeshFilter>();
        arrowMF.mesh = GizmoMeshFactory.CreateCone(arrowRadius, arrowHeight);
        MeshRenderer arrowMR = arrow.AddComponent<MeshRenderer>();
        arrowMR.material = gizmoMaterial;

        // 添加碰撞体（包围整个轴）
        BoxCollider collider = axisObject.AddComponent<BoxCollider>();
        collider.center = direction * (axisLength * 0.5f);
        collider.size = new Vector3(
            direction.x != 0 ? axisLength + arrowHeight : arrowRadius * 2,
            direction.y != 0 ? axisLength + arrowHeight : arrowRadius * 2,
            direction.z != 0 ? axisLength + arrowHeight : arrowRadius * 2
        );

        // 添加 GizmoHandle 组件
        GizmoHandle handle = axisObject.AddComponent<GizmoHandle>();
        handle.axis = axis;
        // 注意：GizmoHandle 会在 Start/ApplyProperties 中应用颜色到子物体的 Renderer
        // 在这里只需设置初始参数
        handle.normalColor = color;
        handle.highlightColor = highlightColor;
        handle.SetColor(color);

        return handle;
    }

    #endregion

    #region 屏幕恒定大小

    private void UpdateScreenConstantSize()
    {
        if (renderCamera == null || gizmoRoot == null)
            return;

        float scale;

        if (renderCamera.orthographic)
        {
            // 正交相机：Scale = OrthoSize * Coefficient
            scale = renderCamera.orthographicSize * screenSizeCoefficient;
        }
        else
        {
            // 透视相机：Scale = Distance * tan(FOV/2) * Coefficient
            float distance = Vector3.Distance(renderCamera.transform.position, gizmoRoot.transform.position);
            float fovRad = renderCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            scale = distance * Mathf.Tan(fovRad) * screenSizeCoefficient;
        }

        gizmoRoot.transform.localScale = Vector3.one * scale;
    }

    #endregion

    #region 输入处理

    private void HandleInput()
    {
        // 获取鼠标射线
        Ray ray = renderCamera.ScreenPointToRay(Input.mousePosition);

        // 鼠标按下
        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, gizmoLayerMask))
            {
                GizmoHandle handle = hit.collider.GetComponent<GizmoHandle>();
                if (handle != null)
                {
                    BeginDrag(handle, ray);
                }
            }
        }

        // 拖拽中
        if (isDragging && Input.GetMouseButton(0))
        {
            UpdateDrag(ray);
        }

        // 鼠标释放
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }

        // 悬停检测（非拖拽状态下）
        if (!isDragging)
        {
            HandleHover(ray);
        }
    }

    private void HandleHover(Ray ray)
    {
        RaycastHit hit;
        GizmoHandle newHoveredHandle = null;

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, gizmoLayerMask))
        {
            newHoveredHandle = hit.collider.GetComponent<GizmoHandle>();
        }

        // 更新高亮状态
        if (newHoveredHandle != hoveredHandle)
        {
            if (hoveredHandle != null)
            {
                hoveredHandle.SetHighlight(false);
            }

            hoveredHandle = newHoveredHandle;

            if (hoveredHandle != null)
            {
                hoveredHandle.SetHighlight(true);
            }
        }
    }

    #endregion

    #region 拖拽逻辑（射线-平面求交）

    private void BeginDrag(GizmoHandle handle, Ray ray)
    {
        currentDragHandle = handle;
        isDragging = true;

        // 清除悬停高亮
        if (hoveredHandle != null)
        {
            hoveredHandle.SetHighlight(false);
            hoveredHandle = null;
        }

        // 设置拖拽高亮
        currentDragHandle.SetHighlight(true);

        if (gizmoType == GizmoType.Translation)
        {
            // --- 平移模式 ---
            dragPlane = CreateDragPlane(handle);
            float enter;
            if (dragPlane.Raycast(ray, out enter))
            {
                lastWorldPosition = ray.GetPoint(enter);
            }
        }
        else
        {
            // --- 旋转模式 ---
            // 确定旋转轴
            rotationAxis = handle.GetWorldAxisDirection();
            // 旋转拖拽平面：也就是旋转圆环所在的平面 (法线 = 旋转轴)
            dragPlane = new Plane(rotationAxis, gizmoRoot.transform.position);
            
            // 计算初始角度
            startAngle = GetCurrentRotationAngle(ray);
            lastAngle = startAngle;
            currentRotationAngleAccumulator = 0f; // 重置累积角度

            // 激活扇形显示
            if (rotationFanObject != null && fanMaterial != null)
            {
                rotationFanObject.SetActive(true);

                // 计算当前点击点，用于对齐扇形起始边
                float enter;
                if (dragPlane.Raycast(ray, out enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    Vector3 dir = (hitPoint - gizmoRoot.transform.position).normalized;
                    
                    // Align Fan: Z=Normal(Axis), X=StartDir
                    // Y = Cross(Z, X)
                    Vector3 upDir = Vector3.Cross(rotationAxis, dir).normalized;
                    if (upDir.sqrMagnitude > 0.001f)
                    {
                         rotationFanObject.transform.rotation = Quaternion.LookRotation(rotationAxis, upDir);
                    }
                    else
                    {
                        // Fallback
                        rotationFanObject.transform.rotation = Quaternion.FromToRotation(Vector3.forward, rotationAxis);
                    }
                }

                // 记录扇形初始世界旋转，用于后续帧修正
                fanInitialRotation = rotationFanObject.transform.rotation;

                rotationFanObject.transform.position = gizmoRoot.transform.position; 
                rotationFanObject.transform.localScale = Vector3.one; // Reset scale
                
                fanMaterial.SetFloat("_Angle", 0f); // Reset angle
                fanMaterial.SetColor("_Color", handle.normalColor * new Color(1,1,1,0.5f)); 
            }
        }

        // 触发事件
        OnBeginDrag?.Invoke(handle.axis);

        Debug.Log($"[RuntimeGizmoController] 开始拖拽 Mode={gizmoType} Axis={handle.axis}");
    }

    private float GetCurrentRotationAngle(Ray ray)
    {
        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 hitPoint = ray.GetPoint(enter);
            Vector3 center = gizmoRoot.transform.position;
            Vector3 dir = hitPoint - center;
            
            // 为了计算角度，我们需要在这个平面上定义一个 "Up" 和 "Right"
            // Arbitrary axes on the plane
            Vector3 planeNormal = dragPlane.normal;
            Vector3 planeRight = Vector3.Cross(planeNormal, Vector3.up);
            if (planeRight.sqrMagnitude < 0.001f) planeRight = Vector3.Cross(planeNormal, Vector3.right);
            planeRight.Normalize();
            Vector3 planeUp = Vector3.Cross(planeRight, planeNormal).normalized;
            
            // Project dir to plane (redundant if raycast is correct, but safe)
            // Calculate angle using Atan2(y, x)
            float x = Vector3.Dot(dir, planeRight);
            float y = Vector3.Dot(dir, planeUp);
            return Mathf.Atan2(y, x) * Mathf.Rad2Deg;
        }
        return 0f;
    }

    private void UpdateDrag(Ray ray)
    {
        if (currentDragHandle == null)
            return;

        if (gizmoType == GizmoType.Translation)
        {
            // --- 平移更新 ---
            UpdateTranslationDrag(ray);
        }
        else
        {
            // --- 旋转更新 ---
            UpdateRotationDrag(ray);
        }
    }

    private void UpdateTranslationDrag(Ray ray)
    {
        float enter;
        if (dragPlane.Raycast(ray, out enter))
        {
            Vector3 currentWorldPosition = ray.GetPoint(enter);
            Vector3 worldDelta = currentWorldPosition - lastWorldPosition;
            Vector3 constrainedDelta;

            if (currentDragHandle.axis == GizmoHandle.Axis.XY
                || currentDragHandle.axis == GizmoHandle.Axis.XZ
                || currentDragHandle.axis == GizmoHandle.Axis.YZ)
            {
                constrainedDelta = worldDelta;
            }
            else
            {
                Vector3 axisDirection = currentDragHandle.GetWorldAxisDirection();
                constrainedDelta = Vector3.Project(worldDelta, axisDirection);
            }

            if (constrainedDelta.magnitude > 0.0001f)
            {
                OnDrag?.Invoke(constrainedDelta); // 兼容接口，虽然名字叫 OnDrag，但对于旋转它可能不太合适，但我们暂时用这个

                if (targetTransform != null)
                {
                    targetTransform.position += constrainedDelta;
                }
            }
            lastWorldPosition = currentWorldPosition;
        }
    }

    private void UpdateRotationDrag(Ray ray)
    {
        // 修正扇形物体的世界旋转，防止随父物体(GizmoRoot)旋转而偏移
        if (rotationFanObject != null && rotationFanObject.activeSelf)
        {
            rotationFanObject.transform.rotation = fanInitialRotation;
        }

        float currentAngle = GetCurrentRotationAngle(ray);
        
        // 计算 Delta
        float deltaAngle = currentAngle - lastAngle;
        
        // 处理 Atan2 的周期性跳变 (-180 到 180)
        if (deltaAngle > 180f) deltaAngle -= 360f;
        else if (deltaAngle < -180f) deltaAngle += 360f;

        if (Mathf.Abs(deltaAngle) > 0.001f)
        {
            // 更新累积角度
            currentRotationAngleAccumulator += -deltaAngle; 

            // 更新扇形Shader显示
            if (fanMaterial != null)
            {
                // 处理扇形方向
                float absAngle = Mathf.Abs(currentRotationAngleAccumulator);
                // 限制在 360 度内
                absAngle = absAngle % 360f;
                
                fanMaterial.SetFloat("_Angle", absAngle);

                // 根据旋转方向翻转扇形 (Scale Y)
                // 假设 Shader 绘制 0 -> +Angle (CCW in local space)
                // 如果 accum 是负值 (CW)，我们需要翻转 Y 轴
                float scaleY = (currentRotationAngleAccumulator >= 0) ? 1f : -1f;
                rotationFanObject.transform.localScale = new Vector3(1, scaleY, 1);
            }

            // 我们不复用 OnDrag(Vector3) 事件，因为旋转很难用 Vector3 delta 表达
            // 但如果用户需要事件回调，我们最好扩展 Action 定义
            // 在这里我们直接旋转目标
            
            if (targetTransform != null)
            {
                targetTransform.Rotate(rotationAxis, -deltaAngle, Space.World); 
            }
        }

        lastAngle = currentAngle; // 只要有计算就应该更新lastAngle，防止漂移
    }

    private void EndDrag()
    {
        if (currentDragHandle != null)
        {
            currentDragHandle.SetHighlight(false);
        }

        // 隐藏扇形
        if (rotationFanObject != null)
        {
            rotationFanObject.SetActive(false);
        }

        isDragging = false;
        currentDragHandle = null;

        // 触发事件
        OnEndDrag?.Invoke();

        Debug.Log("[RuntimeGizmoController] 结束拖拽");
    }

    /// <summary>
    /// 创建拖拽平面
    /// 平面通过 Gizmo 中心，法线应垂直于移动方向
    /// </summary>
    private Plane CreateDragPlane(GizmoHandle handle)
    {
        Vector3 gizmoPosition = gizmoRoot.transform.position;
        Vector3 planeNormal;
        
        // 获取轴向（对于 Plane 类型，GetAxisDirection() 已经返回了法线）
        if (handle.axis == GizmoHandle.Axis.XY 
            || handle.axis == GizmoHandle.Axis.XZ 
            || handle.axis == GizmoHandle.Axis.YZ)
        {
            // 平面拖拽：法线是固定的世界方向（需考虑 Gizmo 旋转）
            planeNormal = handle.GetWorldAxisDirection();
        }
        else // 单轴拖拽
        {
            Vector3 axisDirection = handle.GetWorldAxisDirection();
            Vector3 toCam = renderCamera.transform.position - gizmoPosition;
            
            // 构建一个包含轴向量且面向相机的平面
            planeNormal = Vector3.Cross(axisDirection, Vector3.Cross(toCam, axisDirection));

            if (planeNormal.sqrMagnitude < 0.001f)
            {
                planeNormal = Vector3.Cross(axisDirection, renderCamera.transform.up);
                if (planeNormal.sqrMagnitude < 0.001f)
                {
                    planeNormal = Vector3.Cross(axisDirection, renderCamera.transform.right);
                }
            }
        }

        planeNormal.Normalize();

        return new Plane(planeNormal, gizmoPosition);
    }

    #endregion

    void OnValidate()
    {
        // 允许在 Inspector 中切换模式实时生效（仅在运行时）
        if (Application.isPlaying)
        {
            // 简单的调用 SetGizmoType 来应用显隐状态切换
            // 我们需要稍微 hack 一下，因为 OnValidate 中不能 destroy/create，但SetActive是安全的
            // 为了避免递归调用，我们手动应用显隐逻辑
            if (translationRoot != null) translationRoot.SetActive(gizmoType == GizmoType.Translation);
            if (rotationRoot != null) rotationRoot.SetActive(gizmoType == GizmoType.Rotation);
            // Scale模式下隐藏所有Gizmo
        }
    }
}
