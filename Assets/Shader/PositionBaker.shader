Shader "Hidden/Tesla_Bake_ObjectPos"
{
    Properties
    {
        // 不需要任何贴图输入，我们只读取模型数据
    }
    SubShader
    {
        // 关闭剔除（防止背面没画出来），关闭深度测试（防止重叠部分乱闪）
        Cull Off 
        ZTest Always 
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION; // 模型的原始 3D 坐标
                float2 uv : TEXCOORD0;    // 模型的 UV
            };

            struct v2f
            {
                float4 vertex : SV_POSITION; // 告诉 GPU 画在屏幕哪里
                float3 objPos : TEXCOORD0;   // 要存储的数据（模型空间坐标）
            };

            v2f vert (appdata v)
            {
                v2f o;

                // ----------------------------------------------------
                // 【核心黑魔法：UV 重映射】
                // 正常的渲染：o.vertex = UnityObjectToClipPos(v.vertex);
                // 烘焙的渲染：直接把 UV 变成屏幕坐标
                // ----------------------------------------------------
                
                // 1. 把 UV (0~1) 映射到 Clip Space (-1~1)
                float2 uvRemapped = v.uv * 2.0 - 1.0;
                
                // 2. 修正 Y 轴 (DirectX 和 OpenGL 的 UV Y轴可能是反的，Unity通常要翻转一下)
                // 这里不仅决定了画面会不会倒过来，也决定了像素对应的位置
                #if UNITY_UV_STARTS_AT_TOP
                uvRemapped.y = -uvRemapped.y;
                #endif

                // 3. 赋值给 SV_POSITION
                // z 设为 0 (贴在屏幕上), w 设为 1
                o.vertex = float4(uvRemapped.x, uvRemapped.y, 0.0, 1.0);

                // ----------------------------------------------------
                // 【传递数据】
                // 我们要把什么数据存进贴图里？这里存的是 Object Space Position
                // ----------------------------------------------------
                o.objPos = v.vertex.xyz; 

                return o;
            }

            // 像素着色器：输出颜色
            float4 frag (v2f i) : SV_Target
            {
                // 把坐标 (x, y, z) 直接当作颜色 (r, g, b) 输出
                // Alpha 通道设为 1，表示这里有数据
                return float4(i.objPos, 1.0);
            }
            ENDCG
        }
    }
}