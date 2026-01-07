using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>
/// 贴纸管理器 - 管理所有贴纸并渲染到RenderTexture
/// </summary>
public class DecalManager : MonoBehaviour
{
    [Header("必需资源")]
    [Tooltip("烘焙的位置图（由TeslaBakingTool生成）")]
    public Texture2D positionMap;
    
    [Tooltip("烘焙的法线图（由TeslaBakingTool生成）")]
    public Texture2D normalMap;
    
    [Tooltip("应用贴纸层的目标材质")]
    public Material targetMaterial;
    
    [Tooltip("贴纸层在材质中的属性名")]
    public string decalLayerPropertyName = "_DecalLayer";
    
    [Header("渲染设置")]
    [Tooltip("贴纸层分辨率")]
    public int resolution = 2048;
    
    [Header("贴纸列表")]
    public List<DecalData> decals = new List<DecalData>();
    
    // 私有变量
    private RenderTexture decalRenderTexture;
    private Material decalMaterial;
    private Mesh quadMesh;
    
    private void Start()
    {
        InitializeSystem();
    }
    
    /// <summary>
    /// 初始化贴纸系统
    /// </summary>
    private void InitializeSystem()
    {
        // 1. 创建RenderTexture（用于存储所有贴纸的合成结果）
        decalRenderTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
        decalRenderTexture.name = "DecalLayer";
        decalRenderTexture.wrapMode = TextureWrapMode.Clamp;
        
        // 清空为透明
        RenderTexture.active = decalRenderTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = null;
        
        // 2. 创建贴纸材质
        Shader decalShader = Shader.Find("Tesla/DecalProjection");
        if (decalShader == null)
        {
            Debug.LogError("❌ 找不到 Shader 'Tesla/DecalProjection'！请检查：\n" +
                          "1. Assets/Shader/DecalProjection.shader 是否存在\n" +
                          "2. 在Project窗口右键 → Reimport 该shader文件\n" +
                          "3. 或者在菜单中选择 Assets → Refresh");
            enabled = false; // 禁用组件避免后续错误
            return;
        }
        
        decalMaterial = new Material(decalShader);
        decalMaterial.SetTexture("_PositionMap", positionMap);
        decalMaterial.SetTexture("_NormalMap", normalMap);
        
        // 3. 创建一个全屏四边形（用于渲染贴纸）
        CreateQuadMesh();
        
        // 4. 将RenderTexture分配给目标材质
        if (targetMaterial != null)
        {
            targetMaterial.SetTexture(decalLayerPropertyName, decalRenderTexture);
            Debug.Log($"✅ 贴纸层已分配到材质属性: {decalLayerPropertyName}");
        }
        
        // 5. 渲染初始贴纸
        RenderAllDecals();
    }
    
    /// <summary>
    /// 创建全屏四边形Mesh
    /// </summary>
    private void CreateQuadMesh()
    {
        quadMesh = new Mesh();
        quadMesh.name = "DecalQuad";
        
        // 顶点（覆盖整个UV空间 0~1）
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(0, 0, 0), // 左下
            new Vector3(1, 0, 0), // 右下
            new Vector3(1, 1, 0), // 右上
            new Vector3(0, 1, 0)  // 左上
        };
        
        // UV
        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(1, 1),
            new Vector2(0, 1)
        };
        
        // 三角形索引
        int[] triangles = new int[6]
        {
            0, 1, 2,
            0, 2, 3
        };
        
        quadMesh.vertices = vertices;
        quadMesh.uv = uvs;
        quadMesh.triangles = triangles;
    }
    
    /// <summary>
    /// 渲染所有贴纸到RenderTexture
    /// </summary>
    public void RenderAllDecals()
    {
        if (decalRenderTexture == null || decalMaterial == null || quadMesh == null)
        {
            Debug.LogWarning("⚠️ 贴纸系统未初始化！");
            return;
        }
        
        // 清空RenderTexture
        RenderTexture.active = decalRenderTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0));
        RenderTexture.active = null;
        
        // 使用CommandBuffer渲染所有贴纸
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "Render Decals";
        
        cmd.SetRenderTarget(decalRenderTexture);
        
        // 遍历所有贴纸
        foreach (var decal in decals)
        {
            if (decal.decalTexture == null) continue;
            
            // 设置shader参数
            decalMaterial.SetTexture("_DecalTex", decal.decalTexture);
            decalMaterial.SetFloat("_Opacity", decal.opacity);
            decalMaterial.SetColor("_TintColor", decal.tintColor);
            decalMaterial.SetMatrix("_DecalProjectionMatrix", decal.GetInverseProjectionMatrix());
            
            // 设置混合模式
            SetBlendMode(decal.blendMode);
            
            // 绘制四边形
            cmd.DrawMesh(quadMesh, Matrix4x4.identity, decalMaterial, 0, 0);
        }
        
        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();
        
        Debug.Log($"✅ 已渲染 {decals.Count} 个贴纸");
    }
    
    /// <summary>
    /// 设置混合模式
    /// </summary>
    private void SetBlendMode(DecalData.BlendMode mode)
    {
        switch (mode)
        {
            case DecalData.BlendMode.AlphaBlend:
                decalMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                decalMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                break;
                
            case DecalData.BlendMode.Additive:
                decalMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                decalMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                break;
                
            case DecalData.BlendMode.Multiply:
                decalMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.DstColor);
                decalMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
                break;
        }
    }
    
    /// <summary>
    /// 添加贴纸
    /// </summary>
    public void AddDecal(DecalData decal)
    {
        decals.Add(decal);
        RenderAllDecals();
    }
    
    /// <summary>
    /// 移除贴纸
    /// </summary>
    public void RemoveDecal(DecalData decal)
    {
        decals.Remove(decal);
        RenderAllDecals();
    }
    
    /// <summary>
    /// 清空所有贴纸
    /// </summary>
    public void ClearAllDecals()
    {
        decals.Clear();
        RenderAllDecals();
    }
    
    /// <summary>
    /// 更新贴纸（当贴纸参数改变时调用）
    /// </summary>
    public void UpdateDecals()
    {
        RenderAllDecals();
    }
    
    private void OnDestroy()
    {
        // 清理资源
        if (decalRenderTexture != null)
        {
            decalRenderTexture.Release();
            Destroy(decalRenderTexture);
        }
        
        if (decalMaterial != null)
        {
            Destroy(decalMaterial);
        }
        
        if (quadMesh != null)
        {
            Destroy(quadMesh);
        }
    }
    
    // Editor调试
    private void OnValidate()
    {
        // 当Inspector中的值改变时，更新贴纸渲染
        if (Application.isPlaying && decalRenderTexture != null)
        {
            UpdateDecals();
        }
    }
}
