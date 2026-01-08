using UnityEngine;

/// <summary>
/// 贴纸数据结构 - 存储单个贴纸的所有参数
/// </summary>
[System.Serializable]
public class DecalData
{
    [Header("贴纸基础信息")]
    public string decalName = "Decal";
    public Texture2D decalTexture;
    
    [Header("3D空间位置")]
    public Vector3 worldPosition; 
    public Vector3 projectionDirection = Vector3.down; // 蓝色轴 (Z)
    
    // [新增] 关键修改：存储上方向向量，替代 float rotation
    // 这代表贴纸的"头顶"朝向 (黄色轴 Y)
    public Vector3 upVector = Vector3.up; 
    
    [Header("贴纸变换")]
    public float size = 0.2f; 
    
    [Header("贴纸属性")]
    [Range(0f, 1f)]
    public float opacity = 1f; 
    public Color tintColor = Color.white; 
    
    [Header("投影设置")]
    public float projectionDepth = 0.1f; 
    
    [Header("混合模式")]
    public BlendMode blendMode = BlendMode.AlphaBlend;
    
    public enum BlendMode
    {
        AlphaBlend,
        Additive,
        Multiply
    }
    
    /// <summary>
    /// 获取贴纸的投影矩阵
    /// </summary>
    public Matrix4x4 GetProjectionMatrix()
    {
        // [修改] 使用双参数 LookRotation
        // 参数1：看向哪里 (Z轴 - 投影方向)
        // 参数2：头顶朝哪 (Y轴 - 你的 TestPoint.up)
        // 这样可以锁定旋转，不会因为接近世界坐标Y轴而乱转
        Quaternion finalRotation = Quaternion.LookRotation(projectionDirection, upVector);
        
        Vector3 scale = new Vector3(size, size, projectionDepth);
        
        return Matrix4x4.TRS(worldPosition, finalRotation, scale);
    }
    
    public Matrix4x4 GetInverseProjectionMatrix()
    {
        return GetProjectionMatrix().inverse;
    }
}