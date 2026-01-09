Shader "Tesla/DecalGizmoPreview"
{
    Properties
    {
        _DecalTex ("Decal Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,0,0.3)
        _TintColor ("Tint Color", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0, 1)) = 0.5
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
        Cull Off
        
        Pass
        {
            Name "DecalGizmoPreview"
            
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
            
            CBUFFER_START(UnityPerMaterial)
                float4 _DecalTex_ST;
                half4 _BaseColor;
                half4 _TintColor;
                float _Opacity;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _DecalTex);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // 采样贴纸纹理
                half4 decalColor = SAMPLE_TEXTURE2D(_DecalTex, sampler_DecalTex, input.uv);
                
                // 应用tint颜色和透明度
                decalColor.rgb *= _TintColor.rgb;
                decalColor.a *= _Opacity;
                
                // 标准alpha混合：底色和贴纸根据贴纸alpha混合
                half4 finalColor;
                finalColor.rgb = lerp(_BaseColor.rgb, decalColor.rgb, decalColor.a);
                finalColor.a = _BaseColor.a + decalColor.a * (1.0 - _BaseColor.a);
                
                return finalColor;
            }
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
