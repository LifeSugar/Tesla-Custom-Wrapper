using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

[ExecuteAlways] 
public class DecalManager : MonoBehaviour
{
    public static DecalManager Instance { get; private set; }

    [Header("必需资源")]
    public Texture2D positionMap;
    public Texture2D normalMap;
    public Transform carRoot;
    
    [Header("目标材质")]
    public Material targetMaterial;
    public string decalLayerPropertyName = "_DecalLayer";
    
    [Header("渲染设置")]
    public int resolution = 2048;
    
    private List<DecalData> decals = new List<DecalData>();
    
    private RenderTexture decalRenderTexture;
    private Material decalMaterial;
    private Mesh quadMesh;
    private bool isDirty = false;

    private void OnEnable()
    {
        
        if (Instance == null) Instance = this;
        else if (Instance != this)
        {
            // 在 Editor 模式下，允许覆盖旧实例（防止脚本重载导致的引用丢失）
            if (!Application.isPlaying) Instance = this;
        }

        InitializeSystem();
        MarkDirty(); 
    }
    
    //在 Inspector 修改 Manager 的参数（如分辨率、贴图）时立即刷新
    private void OnValidate()
    {
        if (Application.isPlaying) return; 
        if (decalRenderTexture != null && decalRenderTexture.width != resolution)
        {
            CleanUp();
            InitializeSystem();
        }
        MarkDirty();
    }

    private void OnDisable()
    {
        CleanUp();
    }

    private void Update()
    {
        // 持续检查资源完整性 (Editor下材质可能会丢)
        if (!Application.isPlaying)
        {
            if (decalMaterial == null || decalRenderTexture == null)
            {
                InitializeSystem();
                isDirty = true;
            }
        }

        if (isDirty)
        {
            RenderAllDecals();
            isDirty = false;
        }
    }

    public void RegisterDecal(DecalData decal)
    {
        if (!decals.Contains(decal))
        {
            decals.Add(decal);
            MarkDirty();
        }
    }

    public void UnregisterDecal(DecalData decal)
    {
        if (decals.Contains(decal))
        {
            decals.Remove(decal);
            MarkDirty();
        }
    }

    public void MarkDirty()
    {
        isDirty = true;
    }

    private void InitializeSystem()
    {
        // 1. 创建 RT
        if (decalRenderTexture == null)
        {
            decalRenderTexture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32);
            decalRenderTexture.name = "DecalLayer";
            decalRenderTexture.wrapMode = TextureWrapMode.Clamp;
            decalRenderTexture.Create(); // 显式 Create
        }

        // 2. 创建材质
        if (decalMaterial == null)
        {
            Shader decalShader = Shader.Find("Tesla/DecalProjection");
            if (decalShader != null)
            {
                decalMaterial = new Material(decalShader);
                // 确保材质立即拥有贴图数据
                if (positionMap != null) decalMaterial.SetTexture("_PositionMap", positionMap);
                if (normalMap != null) decalMaterial.SetTexture("_NormalMap", normalMap);
            }
        }
        else
        {
            // 如果材质已存在，重新赋值贴图防止丢失
            if (positionMap != null) decalMaterial.SetTexture("_PositionMap", positionMap);
            if (normalMap != null) decalMaterial.SetTexture("_NormalMap", normalMap);
        }

        // 3. 创建 Mesh
        if (quadMesh == null) CreateQuadMesh();

        // 4. 绑定到目标车漆
        if (targetMaterial != null)
        {
            targetMaterial.SetTexture(decalLayerPropertyName, decalRenderTexture);
        }
    }

    private void RenderAllDecals()
    {
        if (decalRenderTexture == null || decalMaterial == null || quadMesh == null) return;

        // 清空 RT
        RenderTexture.active = decalRenderTexture;
        GL.Clear(true, true, new Color(0, 0, 0, 0)); 
        RenderTexture.active = null;

        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "Render Decals";
        cmd.SetRenderTarget(decalRenderTexture);

        foreach (var decal in decals)
        {
            if (decal.decalTexture == null) continue;

            // 坐标转换逻辑 (保持之前的修复)
            Vector3 decalPosOS = carRoot != null ? carRoot.InverseTransformPoint(decal.worldPosition) : decal.worldPosition;
            Vector3 decalDirOS = carRoot != null ? carRoot.InverseTransformDirection(decal.projectionDirection) : decal.projectionDirection;
            Vector3 decalUpOS = carRoot != null ? carRoot.InverseTransformDirection(decal.upVector) : decal.upVector;
            
            if (decalDirOS == Vector3.zero) decalDirOS = Vector3.forward;

            // 旋转构建
            Quaternion finalRot = Quaternion.LookRotation(decalDirOS, decalUpOS);
            
            Vector3 scale = new Vector3(decal.size, decal.size, decal.projectionDepth);
            Matrix4x4 trs = Matrix4x4.TRS(decalPosOS, finalRot, scale);
            Matrix4x4 invProjectionMatrix = trs.inverse;

            // 设置属性
            decalMaterial.SetTexture("_DecalTex", decal.decalTexture);
            decalMaterial.SetFloat("_Opacity", decal.opacity);
            decalMaterial.SetColor("_TintColor", decal.tintColor);
            decalMaterial.SetMatrix("_DecalProjectionMatrix", invProjectionMatrix);
            decalMaterial.SetVector("_ProjectionDirOS", new Vector4(decalDirOS.x, decalDirOS.y, decalDirOS.z, 0));

            // 设置混合模式
            switch (decal.blendMode)
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

            cmd.DrawMesh(quadMesh, Matrix4x4.identity, decalMaterial);
        }

        Graphics.ExecuteCommandBuffer(cmd);
        cmd.Release();
    }

    private void CreateQuadMesh()
    {
        quadMesh = new Mesh { name = "DecalQuad" };
        quadMesh.vertices = new Vector3[] { new Vector3(0,0,0), new Vector3(1,0,0), new Vector3(1,1,0), new Vector3(0,1,0) };
        quadMesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
        quadMesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
    }

    private void CleanUp()
    {
        if (decalRenderTexture != null) decalRenderTexture.Release();
        if (decalMaterial != null) DestroyImmediate(decalMaterial);
        if (quadMesh != null) DestroyImmediate(quadMesh);
    }
}