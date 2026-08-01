Shader "UI/Pseudo3DRoad"
{
    Properties
    {
        [PerRendererData] _MainTex ("Road Texture", 2D) = "white" {}
        _Color ("Road Tint", Color) = (1,1,1,1)
        
        _CurveAmount ("Curve Amount", Float) = 0
        _ScrollOffset ("Scroll Offset", Float) = 0

        _RoadWidthScale ("Road Base Width", Float) = 1.0
        _TilingY ("Z-Depth Tiling", Float) = 2.0
        _HorizonY ("Horizon Height (0~1)", Range(0.1, 1.0)) = 1.0
        
        // 지평선 기준 위로 얼마나 올라가야 그라데이션(Top Color)이 시작될지 정하는 오프셋
        _SkyGradientOffset ("Sky Gradient Offset (0~1)", Range(0.0, 1.0)) = 0.0

        _SkyTopColor ("Sky Top Color", Color) = (0.1, 0.2, 0.5, 1)
        _SkyBottomColor ("Sky Bottom Color", Color) = (0.8, 0.4, 0.2, 1)

        // UI Masking
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 texcoord : TEXCOORD0; };

            sampler2D _MainTex;
            fixed4 _Color, _SkyTopColor, _SkyBottomColor;
            
            // 변수 선언부에 새로 추가한 오프셋 변수 포함
            float _CurveAmount, _ScrollOffset, _RoadWidthScale, _TilingY, _HorizonY, _SkyGradientOffset;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float y = IN.texcoord.y;

                // 그라데이션이 시작되는 실제 Y 좌표 = 지평선 높이 + 추가 오프셋
                float gradientStart = _HorizonY + _SkyGradientOffset;
                
                // y좌표가 gradientStart부터 화면 끝(1.0) 사이일 때만 0.0 ~ 1.0 비율로 섞이도록 계산
                // max(..., 0.0001)은 Division by Zero 방지용
                float skyProgress = clamp((y - gradientStart) / max(1.0 - gradientStart, 0.0001), 0.0, 1.0);
                
                fixed4 bgColor = lerp(_SkyBottomColor, _SkyTopColor, skyProgress);

                if (y >= _HorizonY) return bgColor;

                float depth = max((_HorizonY - y) / _HorizonY, 0.001);
                float z = 1.0 / depth;
                float currentWidth = depth * _RoadWidthScale;
                float curve = (y * y) * _CurveAmount;

                float2 finalUV;
                finalUV.x = (IN.texcoord.x - 0.5 - curve) / currentWidth + 0.5;
                finalUV.y = z * _TilingY + _ScrollOffset;

                // 도로 양옆 빈 공간에도 배경색을 칠함
                if (finalUV.x < 0.0 || finalUV.x > 1.0)
                {
                    return bgColor;
                }

                return tex2D(_MainTex, finalUV) * IN.color * _Color;
            }
            ENDCG
        }
    }
}