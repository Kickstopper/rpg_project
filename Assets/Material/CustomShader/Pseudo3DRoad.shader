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

        _SkyBands ("Sky Color Bands (Stripes)", Range(2, 64)) = 8
        _DitherStrength ("Dither Strength", Range(0.0, 2.0)) = 1.0

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
            float _SkyBands, _DitherStrength;

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
                
                // 도로의 원근 곡률 (이차함수 곡선)
                float hillOffset = (y * y) * _HillAmount;
                float roadY = y - hillOffset; 

                // 0.3배로 부드럽게 스크롤
                float skyY = y + (_HillAmount * 0.3);

                float gradientStart = _HorizonY + _SkyGradientOffset;
                
                // 지평선 위치에 따라 그라데이션 영역이 0에 수렴하여 극단적인 변화가 일어나지 않도록
                // 최소 30%(0.3) 두께의 그라데이션 층이 항상 유지되도록 보장
                float denominator = max(1.0 - gradientStart, 0.3);
                float skyProgress = clamp((skyY - gradientStart) / denominator, 0.0, 1.0);
                
                float xFmod = fmod(IN.vertex.x, 2.0);
                float yFmod = fmod(IN.vertex.y, 2.0);
                float dither = (xFmod * 0.5 + yFmod * 0.25) - 0.375;
                
                float ditheredProgress = skyProgress + (dither * _DitherStrength * (2.0 / _SkyBands));
                float bandedProgress = floor(ditheredProgress * _SkyBands) / max(1.0, _SkyBands - 1.0);
                bandedProgress = saturate(bandedProgress);

                fixed4 bgColor = lerp(_SkyBottomColor, _SkyTopColor, bandedProgress);

                if (roadY >= _HorizonY) 
                {
                    return fixed4(bgColor.rgb, _TransparentSky > 0.5 ? 0.0 : 1.0);
                }

                float depth = max((_HorizonY - roadY) / _HorizonY, 0.001);
                float z = 1.0 / depth;
                float currentWidth = depth * _RoadWidthScale;
                float curve = (roadY * roadY) * _CurveAmount;

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