Shader "UI/Pseudo3DRoad"
{
    Properties
    {
        [PerRendererData] _MainTex ("Road Texture", 2D) = "white" {}
        _Color ("Road Tint", Color) = (1,1,1,1)
        
        _CurveAmount ("Curve Amount", Float) = 0
        _HillAmount ("Hill Amount", Float) = 0 
        _ScrollOffset ("Scroll Offset", Float) = 0

        _RoadWidthScale ("Road Base Width", Float) = 1.0
        _TilingY ("Z-Depth Tiling", Float) = 2.0
        _HorizonY ("Horizon Height (0~1)", Range(0.1, 1.0)) = 1.0
        
        _SkyGradientOffset ("Sky Gradient Offset (0~1)", Range(0.0, 1.0)) = 0.15
        _SkyTopColor ("Sky Top Color", Color) = (0.1, 0.2, 0.5, 1)
        _SkyBottomColor ("Sky Bottom Color", Color) = (0.8, 0.4, 0.2, 1)

        // 하늘을 투명하게 뚫는 토글
        [Toggle] _TransparentSky ("Transparent Sky", Float) = 0

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
            
            float _CurveAmount, _HillAmount, _ScrollOffset, _RoadWidthScale, _TilingY, _HorizonY, _SkyGradientOffset, _TransparentSky;

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
                float hillOffset = (y * y) * _HillAmount;
                float adjustedY = y - hillOffset;

                float gradientStart = _HorizonY + _SkyGradientOffset;
                float skyProgress = clamp((adjustedY - gradientStart) / max(1.0 - gradientStart, 0.0001), 0.0, 1.0);
                fixed4 bgColor = lerp(_SkyBottomColor, _SkyTopColor, skyProgress);

                // 지평선 위(하늘) 영역일 때, 토글이 켜져 있으면 알파(투명도)를 0으로 반환
                if (adjustedY >= _HorizonY) 
                {
                    return fixed4(bgColor.rgb, _TransparentSky > 0.5 ? 0.0 : 1.0);
                }

                float depth = max((_HorizonY - adjustedY) / _HorizonY, 0.001);
                float z = 1.0 / depth;
                float currentWidth = depth * _RoadWidthScale;
                float curve = (adjustedY * adjustedY) * _CurveAmount;

                float2 finalUV;
                finalUV.x = (IN.texcoord.x - 0.5 - curve) / currentWidth + 0.5;
                finalUV.y = z * _TilingY + _ScrollOffset;

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