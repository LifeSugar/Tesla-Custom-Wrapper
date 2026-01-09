Shader "Hidden/RuntimeGizmoFan"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 0, 0.5)
        _Angle ("Angle", Range(-360, 360)) = 0
        _Radius ("Radius", Float) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="Overlay+101" "RenderType"="Transparent" "IgnoreProjector"="True" }
        
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _Color;
            float _Angle;
            float _Radius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                return o;
            }

            #define PI 3.14159265359

            fixed4 frag (v2f i) : SV_Target
            {
                // UV 中心偏移到 (0,0) -> 范围 (-0.5, -0.5) to (0.5, 0.5)
                float2 dir = i.uv - float2(0.5, 0.5);
                float dist = length(dir);

                // 圆形遮罩
                // clip 负数会丢弃像素
                // 如果 dist > 0.5 (即 Mesh 边缘)，剔除
                // 使用 smoothstep 做边缘抗锯齿
                float circleAlpha = 1.0 - smoothstep(0.48, 0.5, dist);
                if (circleAlpha <= 0.01) discard;

                // 角度计算
                // atan2(y, x) 返回范围 (-PI, PI]
                // 转换 0 度位置：我们需要从正右 (1,0) 或者正上 (0,1) 开始
                // 这里的 UV (0,0) 在左下，(1,1) 在右上。
                // dir.x 右+, dir.y 上+
                // atan2(y, x) 0度是 +X (右)
                float currentRad = atan2(dir.y, dir.x); 
                float currentDeg = degrees(currentRad); // -180 to 180

                // 将角度映射到 0 -> 360
                // 我们假设起始边也是 +X (右)
                if (currentDeg < 0) currentDeg += 360;

                // 逻辑：显示范围 [0, _Angle]
                // 需要处理 _Angle 正负
                float absAngle = abs(_Angle);
                bool visible = false;

                if (_Angle >= 0)
                {
                    // 逆时针渲染: 0 -> _Angle
                    visible = (currentDeg <= absAngle);
                }
                else
                {
                    // 顺时针渲染: 360-_Angle -> 360
                    // 或者更简单：逻辑上我们要显示的是 -Angle 方向
                    // 如果角度是负的，我们认为是从 0 往反方向走
                    // 在 0-360 坐标系下，-10度是350度。所以范围是 [360+Angle, 360]
                    visible = (currentDeg >= (360 + _Angle));
                }

                if (!visible) discard;

                return fixed4(_Color.rgb, _Color.a * circleAlpha);
            }
            ENDCG
        }
    }
}
