using UnityEngine;

/// <summary>
/// Gizmo 轴句柄组件
/// 挂载在每个轴对象上，用于标识轴向和处理高亮状态
/// </summary>
[RequireComponent(typeof(Collider))]
public class GizmoHandle : MonoBehaviour
{
    /// <summary>
    /// 轴向枚举
    /// </summary>
    public enum Axis
    {
        X,  // 红色轴
        Y,  // 绿色轴
        Z,  // 蓝色轴
        XY, // 蓝色面
        XZ, // 绿色面
        YZ,  // 红色面
        Screen // 屏幕旋转圆（白色）
    }

    [Header("轴向配置")]
    [Tooltip("当前句柄代表的轴向")]
    public Axis axis = Axis.X;

    [Header("颜色配置")]
    [Tooltip("正常状态下的颜色")]
    public Color normalColor = Color.red;

    [Tooltip("鼠标悬停时的高亮颜色")]
    public Color highlightColor = Color.yellow;

    [Header("材质属性名称")]
    private const string COLOR_PROPERTY = "_Color";
    private const string HIGHLIGHT_COLOR_PROPERTY = "_HighlightColor";
    private const string HIGHLIGHT_FACTOR_PROPERTY = "_HighlightFactor";

    private MeshRenderer[] meshRenderers;
    private MaterialPropertyBlock propertyBlock;
    private bool isHighlighted = false;

    void Awake()
    {
        // 获取所有子物体的渲染器（Shaft 和 Arrow）
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
        
        // 初始化颜色
        // 如果是在 Start 中调用 SetColor 可能更保险，但为了确保初始化色正确，这里先尝试设置
        // 但注意外部 RuntimeGizmoController 创建完后会立即调用 SetColor
    }

    void Start()
    {
        // 再次确保颜色被应用（有时 Awake 时子物体列表可能还没准备好? 不，RuntimeGizmoController 是同步创建的）
        ApplyProperties();
    }

    /// <summary>
    /// 获取轴向的单位向量
    /// </summary>
    public Vector3 GetAxisDirection()
    {
        switch (axis)
        {
            case Axis.X:
                return Vector3.right;
            case Axis.Y:
                return Vector3.up;
            case Axis.Z:
                return Vector3.forward;
            // 对于平面类型，返回其法线方向
            case Axis.XY:
                return Vector3.forward; // 法线是 Z
            case Axis.XZ:
                return Vector3.up;      // 法线是 Y
            case Axis.YZ:
                return Vector3.right;   // 法线是 X
            case Axis.Screen:
                // Screen 轴向通常是朝向摄像机，这里返回 forward 作为默认
                return Vector3.forward;
            default:
                return Vector3.zero;
        }
    }

    /// <summary>
    /// 设置基础颜色
    /// </summary>
    public void SetColor(Color color)
    {
        normalColor = color;
        ApplyProperties();
    }

    /// <summary>
    /// 设置高亮状态
    /// </summary>
    /// <param name="highlighted">是否高亮</param>
    public void SetHighlight(bool highlighted)
    {
        if (isHighlighted == highlighted)
            return;

        isHighlighted = highlighted;
        ApplyProperties();
    }

    private void ApplyProperties()
    {
        if (meshRenderers == null || meshRenderers.Length == 0)
        {
            meshRenderers = GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers == null || meshRenderers.Length == 0) return;
        }

        // 设置通用属性
        // 注意：MaterialPropertyBlock 需要先 Get 再 Set 再 SetBlock
        // 但我们要对多个 renderer 设置相同的属性
        
        foreach (var renderer in meshRenderers)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(COLOR_PROPERTY, normalColor);
            propertyBlock.SetColor(HIGHLIGHT_COLOR_PROPERTY, highlightColor);
            propertyBlock.SetFloat(HIGHLIGHT_FACTOR_PROPERTY, isHighlighted ? 1f : 0f);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    /// <summary>
    /// 获取当前是否高亮
    /// </summary>
    public bool IsHighlighted()
    {
        return isHighlighted;
    }

    /// <summary>
    /// 获取轴向的世界空间方向（考虑 Gizmo 的旋转）
    /// </summary>
    public Vector3 GetWorldAxisDirection()
    {
        // 从父物体的变换获取旋转后的轴向
        // 对于平面类型，这将返回面的法线方向
        Transform gizmoRoot = transform.parent != null ? transform.parent : transform;
        return gizmoRoot.TransformDirection(GetAxisDirection());
    }
}
