Shader "UI/Pseudo3DRoad"
{
    Properties
    {
        [PerRendererData] _MainTex ("Road Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _CurveAmount ("Curve Amount", Float) = 0
        _ScrollOffset ("Scroll Offset", Float) = 0

        _RoadWidthScale ("Road Base Width", Float) = 1.0
        _TilingY ("Z-Depth Tiling", Float) = 2.0
        _HorizonY ("Horizon Height (0~1)", Range(0.1, 1.0)) = 1.0
        
        _SideColor ("Side Color", Color) = (0,0,0,0)

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
            fixed4 _Color, _SideColor;
            float _CurveAmount, _ScrollOffset, _RoadWidthScale, _TilingY, _HorizonY;

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
                // y는 화면 아래(0)에서 위(1)를 나타냄
                float y = IN.texcoord.y;

                // 지평선(Horizon) 컷오프 처리
                if (y >= _HorizonY) return _SideColor;

                // 2D 평면에서의 가짜 깊이(Depth) 계산
                // 화면 아래일수록 depth=1 (가까움), 지평선일수록 depth=0 (아득히 멂)
                float depth = (_HorizonY - y) / _HorizonY;
                depth = max(depth, 0.001); // 0 나누기 에러 방지

                // Z축 거리 역산 (멀수록 기하급수적으로 커짐)
                float z = 1.0 / depth;

                // 가로 폭(Scale)이 지평선에 수렴하도록 계산
                float currentWidth = depth * _RoadWidthScale;

                // 커브 값 계산 (화면 위로 갈수록 더 많이 휨)
                float curve = (y * y) * _CurveAmount;

                float2 finalUV;
                
                // X축 원근감: 중앙(0.5)을 기준으로 커브를 적용한 뒤, 폭(currentWidth)으로 나누어 사다리꼴을 만듦
                finalUV.x = (IN.texcoord.x - 0.5 - curve) / currentWidth + 0.5;

                // Y축 원근감: 거리에 비례(z)하여 타일링을 압축시킴 (멀수록 가로선이 촘촘해짐)
                finalUV.y = z * _TilingY + _ScrollOffset;

                // 도로 폭을 벗어난 양옆 처리
                if (finalUV.x < 0.0 || finalUV.x > 1.0)
                {
                    return _SideColor;
                }

                return tex2D(_MainTex, finalUV) * IN.color;
            }
            ENDCG
        }
    }
}