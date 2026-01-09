Shader "Hidden/RuntimeGizmo"
{
    Properties
    {
        _Color ("Main Color", Color) = (1,1,1,1)
        _HighlightColor ("Highlight Color", Color) = (1,1,0,1)
        _HighlightFactor ("Highlight Factor", Range(0, 1)) = 0
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Overlay+100"
            "IgnoreProjector" = "True"
        }
        
        Pass
        {
            // 核心渲染状态：确保始终在最上层显示
            ZTest Always
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };
            
            float4 _Color;
            float4 _HighlightColor;
            float _HighlightFactor;
            
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                // 无光照模式：直接混合颜色
                float4 baseColor = _Color;
                float4 finalColor = lerp(baseColor, _HighlightColor, _HighlightFactor);
                
                // 保持 Alpha
                return finalColor;
            }
            ENDCG
        }
    }
    
    Fallback Off
}
