using UnityEngine;

/// <summary>
/// 可交互的Gizmo - 使用实体Mesh渲染，支持拖拽边和顶点进行缩放
/// </summary>
[RequireComponent(typeof(DecalPoint))]
public class InteractiveGizmo : MonoBehaviour
{
    [Header("可视化设置")]
    public bool showGizmo = true;

    private DecalPoint testPoint;
    private GameObject gizmoRoot;
    
    // Mesh对象
    private GameObject mainPlane;
    private GameObject framePlane;
    private GameObject depthBox;
    private GameObject[] edgeHandles = new GameObject[4]; // 边中点（左右上下）
    private GameObject[] cornerHandles = new GameObject[4]; // 顶点L形转角
    
    private Material mainMaterial;
    private Material frameMaterial;
    private Material handleMaterial;
    private Material depthBoxMaterial;
    private Material cornerMaterial; // 转角专用材质
    
    // 交互状态
    private bool isDragging = false;
    private int draggingHandleIndex = -1;
    private bool isDraggingCorner = false;
    private Vector3 dragStartMousePos;
    private float dragStartWidth;
    private float dragStartHeight;
    private Camera mainCamera;
    
    // 颜色配置（从DecalManager获取）
    private DecalManager.GizmoColorConfig colorConfig;

    private void Awake()
    {
        testPoint = GetComponent<DecalPoint>();
        mainCamera = Camera.main;
        
        // 从DecalManager获取颜色配置
        if (DecalManager.Instance != null)
        {
            colorConfig = DecalManager.Instance.gizmoColorConfig;
        }
        else
        {
            // 默认配置
            colorConfig = new DecalManager.GizmoColorConfig();
        }
        
        CreateMaterials();
        CreateGizmoMeshes();
    }
    
    private void Start()
    {
        // Start时再次检查并应用配置，确保正确
        if (DecalManager.Instance != null)
        {
            ApplyColorConfig(DecalManager.Instance.gizmoColorConfig);
        }
    }

    private void OnEnable()
    {
        UpdateGizmoVisibility();
    }

    private void Update()
    {
        if (!showGizmo || !testPoint.showGizmos)
        {
            if (gizmoRoot != null)
                gizmoRoot.SetActive(false);
            return;
        }

        if (gizmoRoot != null)
            gizmoRoot.SetActive(true);
        
        // 运行时检查DecalManager配置是否有变化
        if (DecalManager.Instance != null && DecalManager.Instance.gizmoColorConfig != colorConfig)
        {
            ApplyColorConfig(DecalManager.Instance.gizmoColorConfig);
        }

        UpdateGizmoTransforms();
        HandleMouseInput();
    }

    private void CreateMaterials()
    {
        // 创建透明材质用于主平面 - 使用自定义shader显示贴纸
        Shader mainShader = Shader.Find("Tesla/DecalGizmoPreview");
        if (mainShader == null)
        {
            // 如果找不到自定义shader，回退到Unlit
            mainShader = Shader.Find("Universal Render Pipeline/Unlit");
        }
        mainMaterial = new Material(mainShader);
        mainMaterial.SetColor("_BaseColor", colorConfig.mainPlaneColor);
        mainMaterial.SetFloat("_Surface", 1); // Transparent
        mainMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mainMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mainMaterial.SetInt("_ZWrite", 0);
        mainMaterial.renderQueue = 3000;
        
        // 边框材质
        frameMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        frameMaterial.SetColor("_BaseColor", colorConfig.frameColor);
        frameMaterial.renderQueue = 3001;
        
        // 控制点材质
        handleMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        handleMaterial.SetColor("_BaseColor", colorConfig.edgeHandleColor);
        handleMaterial.renderQueue = 3002;
        
        // 转角材质
        cornerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        cornerMaterial.SetColor("_BaseColor", colorConfig.cornerHandleColor);
        cornerMaterial.renderQueue = 3002;
        
        // 深度框材质
        depthBoxMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        depthBoxMaterial.SetColor("_BaseColor", colorConfig.depthBoxColor);
        depthBoxMaterial.SetFloat("_Surface", 1);
        depthBoxMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        depthBoxMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        depthBoxMaterial.SetInt("_ZWrite", 0);
        depthBoxMaterial.renderQueue = 3001;
    }

    private void CreateGizmoMeshes()
    {
        // 创建根节点
        gizmoRoot = new GameObject("GizmoRoot");
        gizmoRoot.transform.SetParent(transform, false);
        gizmoRoot.transform.localPosition = Vector3.zero;
        gizmoRoot.transform.localRotation = Quaternion.identity;
        
        // 创建主平面
        mainPlane = CreateQuad("MainPlane", mainMaterial);
        mainPlane.transform.SetParent(gizmoRoot.transform, false);
        
        // 创建深度框
        depthBox = new GameObject("DepthBox");
        depthBox.transform.SetParent(gizmoRoot.transform, false);
        CreateDepthBoxLines(depthBox.transform);
        
        // 创建边框（4条边）
        framePlane = new GameObject("Frame");
        framePlane.transform.SetParent(gizmoRoot.transform, false);
        CreateFrameLines(framePlane.transform);
        
        // 创建边中点控制柄（4个）
        string[] edgeNames = { "Right", "Top", "Left", "Bottom" };
        for (int i = 0; i < 4; i++)
        {
            edgeHandles[i] = CreateHandle($"EdgeHandle_{edgeNames[i]}", colorConfig.handleSize);
            edgeHandles[i].transform.SetParent(gizmoRoot.transform, false);
        }
        
        // 创建顶点控制柄（4个L形转角）
        string[] cornerNames = { "TopRight", "TopLeft", "BottomLeft", "BottomRight" };
        for (int i = 0; i < 4; i++)
        {
            cornerHandles[i] = CreateLShapeHandle($"CornerHandle_{cornerNames[i]}", colorConfig.cornerHandleSize);
            cornerHandles[i].transform.SetParent(gizmoRoot.transform, false);
        }
    }

    private GameObject CreateQuad(string name, Material mat)
    {
        GameObject quad = new GameObject(name);
        MeshFilter mf = quad.AddComponent<MeshFilter>();
        MeshRenderer mr = quad.AddComponent<MeshRenderer>();
        
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0)
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        
        mf.mesh = mesh;
        mr.material = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        
        return quad;
    }

    private void CreateFrameLines(Transform parent)
    {
        // 使用细长的立方体创建边框
        string[] names = { "Right", "Top", "Left", "Bottom" };
        
        for (int i = 0; i < 4; i++)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = $"Line_{names[i]}";
            line.transform.SetParent(parent, false);
            
            MeshRenderer mr = line.GetComponent<MeshRenderer>();
            mr.material = frameMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            
            // 移除Collider
            Destroy(line.GetComponent<Collider>());
        }
    }
    
    private void CreateDepthBoxLines(Transform parent)
    {
        // 创建12条边构成的立方体框架
        string[] names = { 
            "Front_Right", "Front_Top", "Front_Left", "Front_Bottom", // 前面
            "Back_Right", "Back_Top", "Back_Left", "Back_Bottom", // 后面
            "Vertical_TR", "Vertical_TL", "Vertical_BL", "Vertical_BR" // 垂直边
        };
        
        for (int i = 0; i < 12; i++)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = $"DepthLine_{names[i]}";
            line.transform.SetParent(parent, false);
            
            MeshRenderer mr = line.GetComponent<MeshRenderer>();
            mr.material = depthBoxMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            
            Destroy(line.GetComponent<Collider>());
        }
    }

    private GameObject CreateHandle(string name, float size)
    {
        GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handle.name = name;
        handle.transform.localScale = Vector3.one * size;
        
        MeshRenderer mr = handle.GetComponent<MeshRenderer>();
        mr.material = handleMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        
        // 保留Collider用于点击检测
        SphereCollider collider = handle.GetComponent<SphereCollider>();
        collider.isTrigger = true;
        
        return handle;
    }
    
    private GameObject CreateLShapeHandle(string name, float size)
    {
        GameObject handle = new GameObject(name);
        
        // 创建L形的两个臂
        GameObject arm1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm1.name = "Arm1";
        arm1.transform.SetParent(handle.transform, false);
        arm1.transform.localScale = new Vector3(size * 0.3f, size, size * 0.3f);
        arm1.transform.localPosition = new Vector3(0, size * 0.5f, 0);
        
        GameObject arm2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        arm2.name = "Arm2";
        arm2.transform.SetParent(handle.transform, false);
        arm2.transform.localScale = new Vector3(size, size * 0.3f, size * 0.3f);
        arm2.transform.localPosition = new Vector3(size * 0.5f, 0, 0);
        
        // 设置材质
        arm1.GetComponent<MeshRenderer>().material = cornerMaterial;
        arm1.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arm2.GetComponent<MeshRenderer>().material = cornerMaterial;
        arm2.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        
        // 添加组合碰撞体
        BoxCollider collider1 = arm1.GetComponent<BoxCollider>();
        collider1.isTrigger = true;
        BoxCollider collider2 = arm2.GetComponent<BoxCollider>();
        collider2.isTrigger = true;
        
        // 在父对象上添加一个包围整个L形的碰撞体
        BoxCollider parentCollider = handle.AddComponent<BoxCollider>();
        parentCollider.isTrigger = true;
        parentCollider.center = new Vector3(size * 0.25f, size * 0.25f, 0);
        parentCollider.size = new Vector3(size * 1.2f, size * 1.2f, size * 0.5f);
        
        return handle;
    }

    private void UpdateGizmoTransforms()
    {
        if (gizmoRoot == null) return;
        
        float width = testPoint.decalWidth;
        float height = testPoint.decalHeight;
        float depth = testPoint.projectionDepth;
        
        // 计算相机空间固定线框粗细
        float distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);
        float lineThickness = distanceToCamera * colorConfig.lineThicknessScale; // 使用配置的系数
        
        // 更新主平面大小
        mainPlane.transform.localScale = new Vector3(width, height, 1f);
        mainPlane.transform.localPosition = Vector3.zero;
        
        // 更新主平面贴纸纹理（无贴纸时完全透明）
        if (testPoint.decalTexture != null && colorConfig.showDecalPreview)
        {
            mainMaterial.SetTexture("_BaseMap", testPoint.decalTexture);
            mainMaterial.SetTexture("_DecalTex", testPoint.decalTexture);
            Color tintedColor = testPoint.tintColor * colorConfig.mainPlaneColor;
            // mainPlaneColor.a控制底色透明度，decalPreviewOpacity控制贴纸透明度
            mainMaterial.SetColor("_TintColor", testPoint.tintColor);
            mainMaterial.SetColor("_BaseColor", tintedColor);
            mainMaterial.SetFloat("_Opacity", colorConfig.decalPreviewOpacity);
        }
        else
        {
            // 无贴纸时显示半透明平面
            mainMaterial.SetTexture("_BaseMap", null);
            mainMaterial.SetTexture("_DecalTex", null);
            Color transparent = colorConfig.mainPlaneColor;
            mainMaterial.SetColor("_BaseColor", transparent);
            mainMaterial.SetFloat("_Opacity", transparent.a);
        }
        
        // 更新边框
        Transform[] lines = framePlane.GetComponentsInChildren<Transform>();
        int lineIndex = 0;
        foreach (Transform line in lines)
        {
            if (line == framePlane.transform) continue;
            
            switch (lineIndex)
            {
                case 0: // Right
                    line.localPosition = new Vector3(width * 0.5f, 0, 0);
                    line.localScale = new Vector3(lineThickness, height, lineThickness);
                    break;
                case 1: // Top
                    line.localPosition = new Vector3(0, height * 0.5f, 0);
                    line.localScale = new Vector3(width, lineThickness, lineThickness);
                    break;
                case 2: // Left
                    line.localPosition = new Vector3(-width * 0.5f, 0, 0);
                    line.localScale = new Vector3(lineThickness, height, lineThickness);
                    break;
                case 3: // Bottom
                    line.localPosition = new Vector3(0, -height * 0.5f, 0);
                    line.localScale = new Vector3(width, lineThickness, lineThickness);
                    break;
            }
            lineIndex++;
        }
        
        // 更新深度框
        if (depthBox != null && colorConfig.showDepthBox)
        {
            depthBox.SetActive(true);
            Transform[] depthLines = depthBox.GetComponentsInChildren<Transform>();
            int depthLineIndex = 0;
            foreach (Transform line in depthLines)
            {
                if (line == depthBox.transform) continue;
                
                float halfWidth = width * 0.5f;
                float halfHeight = height * 0.5f;
                
                switch (depthLineIndex)
                {
                    // 前面四条边 (z=0)
                    case 0: // Front_Right
                        line.localPosition = new Vector3(halfWidth, 0, 0);
                        line.localScale = new Vector3(lineThickness, height, lineThickness);
                        break;
                    case 1: // Front_Top
                        line.localPosition = new Vector3(0, halfHeight, 0);
                        line.localScale = new Vector3(width, lineThickness, lineThickness);
                        break;
                    case 2: // Front_Left
                        line.localPosition = new Vector3(-halfWidth, 0, 0);
                        line.localScale = new Vector3(lineThickness, height, lineThickness);
                        break;
                    case 3: // Front_Bottom
                        line.localPosition = new Vector3(0, -halfHeight, 0);
                        line.localScale = new Vector3(width, lineThickness, lineThickness);
                        break;
                    // 后面四条边 (z=depth)
                    case 4: // Back_Right
                        line.localPosition = new Vector3(halfWidth, 0, depth);
                        line.localScale = new Vector3(lineThickness, height, lineThickness);
                        break;
                    case 5: // Back_Top
                        line.localPosition = new Vector3(0, halfHeight, depth);
                        line.localScale = new Vector3(width, lineThickness, lineThickness);
                        break;
                    case 6: // Back_Left
                        line.localPosition = new Vector3(-halfWidth, 0, depth);
                        line.localScale = new Vector3(lineThickness, height, lineThickness);
                        break;
                    case 7: // Back_Bottom
                        line.localPosition = new Vector3(0, -halfHeight, depth);
                        line.localScale = new Vector3(width, lineThickness, lineThickness);
                        break;
                    // 四条垂直边
                    case 8: // Vertical_TR
                        line.localPosition = new Vector3(halfWidth, halfHeight, depth * 0.5f);
                        line.localScale = new Vector3(lineThickness, lineThickness, depth);
                        break;
                    case 9: // Vertical_TL
                        line.localPosition = new Vector3(-halfWidth, halfHeight, depth * 0.5f);
                        line.localScale = new Vector3(lineThickness, lineThickness, depth);
                        break;
                    case 10: // Vertical_BL
                        line.localPosition = new Vector3(-halfWidth, -halfHeight, depth * 0.5f);
                        line.localScale = new Vector3(lineThickness, lineThickness, depth);
                        break;
                    case 11: // Vertical_BR
                        line.localPosition = new Vector3(halfWidth, -halfHeight, depth * 0.5f);
                        line.localScale = new Vector3(lineThickness, lineThickness, depth);
                        break;
                }
                depthLineIndex++;
            }
        }
        else if (depthBox != null)
        {
            depthBox.SetActive(false);
        }
        
        // 更新控制点大小（基于相机距离）
        float scaledHandleSize = colorConfig.handleSize * distanceToCamera * 0.1f;
        float scaledCornerSize = colorConfig.cornerHandleSize * distanceToCamera * 0.1f;
        
        // 更新边中点控制柄位置
        edgeHandles[0].transform.localPosition = new Vector3(width * 0.5f, 0, 0);      // Right
        edgeHandles[0].transform.localScale = Vector3.one * scaledHandleSize;
        edgeHandles[1].transform.localPosition = new Vector3(0, height * 0.5f, 0);     // Top
        edgeHandles[1].transform.localScale = Vector3.one * scaledHandleSize;
        edgeHandles[2].transform.localPosition = new Vector3(-width * 0.5f, 0, 0);     // Left
        edgeHandles[2].transform.localScale = Vector3.one * scaledHandleSize;
        edgeHandles[3].transform.localPosition = new Vector3(0, -height * 0.5f, 0);    // Bottom
        edgeHandles[3].transform.localScale = Vector3.one * scaledHandleSize;
        
        // 更新顶点控制柄位置和旋转（L形需要正确朝向，朝向矩形内部）
        cornerHandles[0].transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0);      // TopRight
        cornerHandles[0].transform.localScale = Vector3.one * scaledCornerSize;
        cornerHandles[0].transform.localRotation = Quaternion.Euler(0, 0, 180); // L朝左下（内部）
        
        cornerHandles[1].transform.localPosition = new Vector3(-width * 0.5f, height * 0.5f, 0);     // TopLeft
        cornerHandles[1].transform.localScale = Vector3.one * scaledCornerSize;
        cornerHandles[1].transform.localRotation = Quaternion.Euler(0, 0, -90); // L朝右下（内部）
        
        cornerHandles[2].transform.localPosition = new Vector3(-width * 0.5f, -height * 0.5f, 0);    // BottomLeft
        cornerHandles[2].transform.localScale = Vector3.one * scaledCornerSize;
        cornerHandles[2].transform.localRotation = Quaternion.Euler(0, 0, 0); // L朝右上（内部）
        
        cornerHandles[3].transform.localPosition = new Vector3(width * 0.5f, -height * 0.5f, 0);     // BottomRight
        cornerHandles[3].transform.localScale = Vector3.one * scaledCornerSize;
        cornerHandles[3].transform.localRotation = Quaternion.Euler(0, 0, 90); // L朝左上（内部）
    }

    private void HandleMouseInput()
    {
        if (mainCamera == null) return;
        
        if (Input.GetMouseButtonDown(0))
        {
            TryStartDrag();
        }
        else if (Input.GetMouseButton(0) && isDragging)
        {
            UpdateDrag();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            StopDrag();
        }
    }

    private void TryStartDrag()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // 检测边中点
        for (int i = 0; i < edgeHandles.Length; i++)
        {
            if (edgeHandles[i].GetComponent<Collider>().Raycast(ray, out hit, 1000f))
            {
                StartEdgeDrag(i);
                return;
            }
        }
        
        // 检测顶点
        for (int i = 0; i < cornerHandles.Length; i++)
        {
            if (cornerHandles[i].GetComponent<Collider>().Raycast(ray, out hit, 1000f))
            {
                StartCornerDrag(i);
                return;
            }
        }
    }

    private void StartEdgeDrag(int edgeIndex)
    {
        isDragging = true;
        isDraggingCorner = false;
        draggingHandleIndex = edgeIndex;
        dragStartMousePos = Input.mousePosition;
        dragStartWidth = testPoint.decalWidth;
        dragStartHeight = testPoint.decalHeight;
        
        // 按住时变色
        handleMaterial.SetColor("_BaseColor", colorConfig.handleDraggingColor);
    }

    private void StartCornerDrag(int cornerIndex)
    {
        isDragging = true;
        isDraggingCorner = true;
        draggingHandleIndex = cornerIndex;
        dragStartMousePos = Input.mousePosition;
        dragStartWidth = testPoint.decalWidth;
        dragStartHeight = testPoint.decalHeight;
        
        // 按住时变色
        cornerMaterial.SetColor("_BaseColor", colorConfig.handleDraggingColor);
    }

    private void UpdateDrag()
    {
        Vector3 mouseDelta = Input.mousePosition - dragStartMousePos;
        
        if (isDraggingCorner)
        {
            // 顶点拖拽：等比缩放
            float screenDelta = mouseDelta.magnitude * Mathf.Sign(mouseDelta.x + mouseDelta.y);
            float scaleFactor = 1f + screenDelta * 0.001f;
            scaleFactor = Mathf.Max(0.01f, scaleFactor);
            
            testPoint.decalWidth = dragStartWidth * scaleFactor;
            testPoint.decalHeight = dragStartHeight * scaleFactor;
        }
        else
        {
            // 边拖拽：单轴缩放
            Vector3 worldDelta = mainCamera.ScreenToWorldPoint(new Vector3(mouseDelta.x, mouseDelta.y, 10f)) 
                               - mainCamera.ScreenToWorldPoint(new Vector3(0, 0, 10f));
            
            switch (draggingHandleIndex)
            {
                case 0: // Right - 改变宽度
                case 2: // Left - 改变宽度
                    float widthDelta = Vector3.Dot(worldDelta, transform.right);
                    testPoint.decalWidth = Mathf.Max(0.01f, dragStartWidth + widthDelta * (draggingHandleIndex == 0 ? 2f : -2f));
                    break;
                    
                case 1: // Top - 改变高度
                case 3: // Bottom - 改变高度
                    float heightDelta = Vector3.Dot(worldDelta, transform.up);
                    testPoint.decalHeight = Mathf.Max(0.01f, dragStartHeight + heightDelta * (draggingHandleIndex == 1 ? 2f : -2f));
                    break;
            }
        }
        
        // 强制刷新贴纸显示
        testPoint.ForceRefresh();
    }

    private void StopDrag()
    {
        isDragging = false;
        isDraggingCorner = false;
        draggingHandleIndex = -1;
        
        // 恢复颜色
        handleMaterial.SetColor("_BaseColor", colorConfig.edgeHandleColor);
        cornerMaterial.SetColor("_BaseColor", colorConfig.cornerHandleColor);
    }

    private void UpdateGizmoVisibility()
    {
        if (gizmoRoot != null)
        {
            gizmoRoot.SetActive(showGizmo && testPoint.showGizmos);
        }
    }
    
    /// <summary>
    /// 应用颜色配置
    /// </summary>
    public void ApplyColorConfig(DecalManager.GizmoColorConfig config)
    {
        colorConfig = config;
        
        // 更新所有材质颜色
        if (mainMaterial != null)
            mainMaterial.SetColor("_BaseColor", colorConfig.mainPlaneColor);
        if (frameMaterial != null)
            frameMaterial.SetColor("_BaseColor", colorConfig.frameColor);
        if (handleMaterial != null)
            handleMaterial.SetColor("_BaseColor", colorConfig.edgeHandleColor);
        if (cornerMaterial != null)
            cornerMaterial.SetColor("_BaseColor", colorConfig.cornerHandleColor);
        if (depthBoxMaterial != null)
            depthBoxMaterial.SetColor("_BaseColor", colorConfig.depthBoxColor);
    }

    private void OnDestroy()
    {
        if (mainMaterial != null) Destroy(mainMaterial);
        if (frameMaterial != null) Destroy(frameMaterial);
        if (handleMaterial != null) Destroy(handleMaterial);
        if (cornerMaterial != null) Destroy(cornerMaterial);
        if (depthBoxMaterial != null) Destroy(depthBoxMaterial);
        if (gizmoRoot != null) Destroy(gizmoRoot);
    }

    private void OnValidate()
    {
        // 如果colorConfig存在，重新应用
        if (colorConfig != null)
        {
            if (mainMaterial != null)
                mainMaterial.SetColor("_BaseColor", colorConfig.mainPlaneColor);
            if (frameMaterial != null)
                frameMaterial.SetColor("_BaseColor", colorConfig.frameColor);
            if (handleMaterial != null)
                handleMaterial.SetColor("_BaseColor", colorConfig.edgeHandleColor);
            if (depthBoxMaterial != null)
                depthBoxMaterial.SetColor("_BaseColor", colorConfig.depthBoxColor);
        }
    }
}
