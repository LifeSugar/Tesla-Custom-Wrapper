Shader "Tesla/DecalProjection"
{
    Properties
    {
        _DecalTex ("Decal Texture", 2D) = "white" {}
        _PositionMap ("Position Map (EXR)", 2D) = "black" {}
        _NormalMap ("Normal Map (EXR)", 2D) = "bump" {}
        
        _Opacity ("Opacity", Range(0, 1)) = 1
        _TintColor ("Tint Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest Always
        Cull Off
        
        Pass
        {
            Name "DecalProjection"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            TEXTURE2D(_DecalTex);
            SAMPLER(sampler_DecalTex);
            
            TEXTURE2D(_PositionMap);
            SAMPLER(sampler_PositionMap);
            
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _DecalTex_ST;
                float _Opacity;
                half4 _TintColor;
                float4x4 _DecalProjectionMatrix;
                float4 _ProjectionDirOS;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 使用UV直接映射到屏幕空间
                float2 uvRemapped = input.uv * 2.0 - 1.0;
                
                #if UNITY_UV_STARTS_AT_TOP
                uvRemapped.y = -uvRemapped.y;
                #endif
                
                output.positionCS = float4(uvRemapped.x, uvRemapped.y, 0.0, 1.0);
                output.uv = input.uv;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 1. 从PositionMap读取该UV位置对应的3D坐标（模型空间）
                float4 objPosData = SAMPLE_TEXTURE2D(_PositionMap, sampler_PositionMap, input.uv);
                
                // 检查是否有有效数据（alpha > 0表示有数据）
                if (objPosData.a < 0.01)
                {
                    discard; // 没有数据的地方不绘制
                }
                
                float3 objPos = objPosData.xyz;
                
                // 2. 将3D坐标转换到贴纸的投影空间
                // 投影空间：贴纸中心为原点，XY平面是贴纸平面，范围[-0.5, 0.5]
                float4 decalSpacePos = mul(_DecalProjectionMatrix, float4(objPos, 1.0));
                
                // 3. 检查是否在投影盒子内（culling）
                // X和Y范围：[-0.5, 0.5]，Z范围：[0, 1]（投影深度）
                if (abs(decalSpacePos.x) > 0.5 || abs(decalSpacePos.y) > 0.5 || 
                    decalSpacePos.z < 0 || decalSpacePos.z > 1)
                {
                    discard; // 超出投影范围
                }
                
                // 4. 将投影空间坐标转换为贴纸UV（0~1）
                float2 decalUV = decalSpacePos.xy + 0.5;
                
                // 5. 采样贴纸纹理
                half4 decalColor = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, decalUV);
                
                // 6. 应用透明度和着色
                decalColor.rgb *= _TintColor.rgb;
                decalColor.a *= _Opacity * _TintColor.a;
                
                // 7. 法线检查（可选）- 防止贴纸出现在背面
                float3 surfaceNormalOS = normalize(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv).xyz);
                float3 projectionDirOS = normalize(_ProjectionDirOS.xyz);
                float normalDot = dot(surfaceNormalOS, -projectionDirOS);
                
                // 如果法线和投影方向相反（> 90度），淡出贴纸
                if (normalDot < 0.1)
                {
                    decalColor.a *= saturate(normalDot * 10); // 平滑过渡
                }
                
                return decalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
