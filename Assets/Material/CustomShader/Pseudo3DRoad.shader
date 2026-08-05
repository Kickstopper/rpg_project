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

        // 레트로 하늘 효과 설정
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
                float hillOffset = (y * y) * _HillAmount;
                float adjustedY = y - hillOffset;

                // 부드러운 진행도(0.0 ~ 1.0) 계산
                float gradientStart = _HorizonY + _SkyGradientOffset;
                float skyProgress = clamp((adjustedY - gradientStart) / max(1.0 - gradientStart, 0.0001), 0.0, 1.0);
                
                // 화면 픽셀 좌표를 이용한 클래식 2x2 Bayer 디더링 패턴 생성
                // 모니터의 실제 픽셀 단위로 체크무늬 패턴을 만듦
                float xFmod = fmod(IN.vertex.x, 2.0);
                float yFmod = fmod(IN.vertex.y, 2.0);
                float dither = (xFmod * 0.5 + yFmod * 0.25) - 0.375;
                
                // 진행도에 디더링 노이즈 섞기
                float ditheredProgress = skyProgress + (dither * _DitherStrength * (2.0 / _SkyBands));

                // 연속적인 값을 지정한 스트라이프 층(예: 8단계)으로 강제로 쪼개어 계단 현상을 만듦
                float bandedProgress = floor(ditheredProgress * _SkyBands) / max(1.0, _SkyBands - 1.0);
                bandedProgress = saturate(bandedProgress);

                // 계단 현상이 적용된 값으로 최종 색상 섞기
                fixed4 bgColor = lerp(_SkyBottomColor, _SkyTopColor, bandedProgress);

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