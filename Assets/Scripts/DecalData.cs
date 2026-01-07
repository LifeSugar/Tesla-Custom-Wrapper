using UnityEngine;

/// <summary>
/// 贴纸数据结构 - 存储单个贴纸的所有参数
/// </summary>
[System.Serializable]
public class DecalData
{
    [Header("贴纸基础信息")]
    public string decalName = "Decal";
    public Texture2D decalTexture; // 贴纸素材图片
    
    [Header("3D空间位置")]
    public Vector3 worldPosition; // 贴纸在3D空间中的位置
    public Vector3 projectionDirection = Vector3.down; // 投影方向（通常是表面法线的反方向）
    
    [Header("贴纸变换")]
    public float size = 0.2f; // 贴纸大小（单位：米）
    public float rotation = 0f; // 贴纸旋转角度（度）
    
    [Header("贴纸属性")]
    [Range(0f, 1f)]
    public float opacity = 1f; // 不透明度
    public Color tintColor = Color.white; // 着色
    
    [Header("投影设置")]
    public float projectionDepth = 0.1f; // 投影深度（防止贴纸被拉伸）
    
    [Header("混合模式")]
    public BlendMode blendMode = BlendMode.AlphaBlend;
    
    public enum BlendMode
    {
        AlphaBlend,  // 标准Alpha混合
        Additive,    // 加法混合
        Multiply     // 正片叠底
    }
    
    /// <summary>
    /// 获取贴纸的投影矩阵
    /// </summary>
    public Matrix4x4 GetProjectionMatrix()
    {
        // 创建投影空间的TRS矩阵
        Quaternion rotation = Quaternion.LookRotation(projectionDirection) * Quaternion.Euler(0, 0, this.rotation);
        Vector3 scale = new Vector3(size, size, projectionDepth);
        
        return Matrix4x4.TRS(worldPosition, rotation, scale);
    }
    
    /// <summary>
    /// 获取逆投影矩阵（用于shader计算）
    /// </summary>
    public Matrix4x4 GetInverseProjectionMatrix()
    {
        return GetProjectionMatrix().inverse;
    }
}
