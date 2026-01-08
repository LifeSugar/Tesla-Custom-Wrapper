Shader "Tesla/CarWithDecals"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _DecalLayer ("Decal Layer", 2D) = "black" {}
        [Toggle] _ShowDecalsOnly ("Show Decals Only (Debug)", Float) = 0
        [Toggle] _ShowUV ("Show UV (Debug)", Float) = 0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_DecalLayer);
            SAMPLER(sampler_DecalLayer);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                float _ShowDecalsOnly;
                float _ShowUV;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.normalWS = normalInput.normalWS;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Debug模式：显示UV
                if (_ShowUV > 0.5)
                {
                    return half4(input.uv.x, input.uv.y, 0, 1.0);
                }
                
                // 基础颜色
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                
                // 贴纸层
                half4 decalColor = SAMPLE_TEXTURE2D(_DecalLayer, sampler_DecalLayer, input.uv);
                
                // Debug模式：仅显示贴纸
                if (_ShowDecalsOnly > 0.5)
                {
                    return half4(decalColor.rgb, 1.0);
                }
                
                // Alpha混合：贴纸覆盖在基础颜色上
                half3 albedo = lerp(baseColor.rgb, decalColor.rgb, decalColor.a);
                
                // Half Lambert 光照计算
                Light mainLight = GetMainLight();
                float3 normalWS = normalize(input.normalWS);
                float NdotL = dot(normalWS, mainLight.direction);
                float halfLambert = NdotL * 0.5 + 0.5;
                
                // 应用光照
                half3 lighting = mainLight.color * halfLambert;
                half3 finalColor = albedo * lighting;
                
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
