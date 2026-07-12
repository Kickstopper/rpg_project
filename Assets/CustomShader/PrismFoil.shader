Shader "Custom/UI/PrismFoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Prism Foil Settings)]
        _PatternTex ("Pattern Mask Texture", 2D) = "white" {} // 사용자 패턴 마스크
        _PrismScale ("Foil Pattern Scale", Float) = 30.0
        _PrismSpeed ("Shimmer Speed", Float) = 1.5
        _PrismIntensity ("Brightness", Range(0.5, 3.0)) = 1.5

        [Header(Shimmer Advanced Settings)]
        [KeywordEnum(Linear, RadialOut, RadialIn)] _FoilPatternType ("Pattern Type", Float) = 0
        _FoilDirection ("Shimmer Direction (for Linear)", Vector) = (1, 1, 0, 0) // normalized vector
        _ShimmerSpread ("Shimmer Spread", Range(0.1, 5.0)) = 1.0 // Shimmer 폭 조절

        // Unity UI Masking & Stencil (UI 렌더링에 필수적인 프로퍼티)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            // KeywordEnum을 위한 multi_compile 추가
            #pragma multi_compile _FOILPATTERNTYPE_LINEAR _FOILPATTERNTYPE_RADIALOUT _FOILPATTERNTYPE_RADIALIN

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _PatternTex;
            fixed4 _Color;
            float4 _ClipRect;
            
            float _PrismScale;
            float _PrismSpeed;
            float _PrismIntensity;
            
            float4 _FoilDirection;
            float _ShimmerSpread;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 foilUV = IN.texcoord * _PrismScale;
                
                // --- [1. 패턴 종류에 따른 Phase 계산] ---
                float phase;
                float timeFactor = _Time.y * _PrismSpeed;

                #if _FOILPATTERNTYPE_LINEAR
                    // 선형 Shimmer: UV 좌표와 방향 벡터의 내적
                    float2 normalDir = normalize(_FoilDirection.xy);
                    // 중앙 기준 좌표를 사용하여 방향을 계산
                    float linearDist = dot(IN.texcoord - float2(0.5, 0.5), normalDir) * _ShimmerSpread;
                    phase = linearDist + timeFactor;
                #elif _FOILPATTERNTYPE_RADIALOUT
                    // 방사형 Out: 중앙에서 거리
                    float radialDist = length(IN.texcoord - float2(0.5, 0.5)) * _ShimmerSpread;
                    phase = -radialDist + timeFactor; // 거리를 뺌으로써 안에서 밖으로
                #elif _FOILPATTERNTYPE_RADIALIN
                    // 방사형 In: 바깥에서 중앙으로
                    float radialDist = length(IN.texcoord - float2(0.5, 0.5)) * _ShimmerSpread;
                    phase = radialDist + timeFactor; // 거리를 더함으로써 밖에서 안으로
                #else
                    phase = timeFactor; // Default or Fallback
                #endif

                // --- [2. 이전과 동일한 Shimmer 효과] ---
                // 각 셀마다 무작위 오프셋 생성
                float2 cell = floor(foilUV);
                float hash = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);
                phase += hash * 1.5;

                // 무지개 빛깔 생성
                fixed3 rainbow = 0.5 + 0.5 * cos(phase + fixed3(0.0, 2.094, 4.188));
                rainbow = pow(rainbow, 1.5);

                // 하이라이트 추가
                float highlight = pow(max(0, cos(phase * 2.0)), 4.0) * 0.4;
                rainbow += highlight;
                rainbow *= _PrismIntensity;

                // 패턴 마스크 적용 (알파 채널 샘플링)
                float shapeMask = tex2D(_PatternTex, foilUV).a; 
                
                fixed3 foilColor = rainbow * shapeMask;

                // 캐릭터(Sprite)와 호일 배경 합성
                fixed4 spriteColor = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                fixed4 finalColor;
                finalColor.rgb = lerp(foilColor, spriteColor.rgb, spriteColor.a);
                finalColor.a = IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (finalColor.a - 0.001);
                #endif

                return finalColor;
            }
        ENDCG
        }
    }
}