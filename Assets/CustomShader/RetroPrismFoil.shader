Shader "Custom/UI/RetroPrismFoil"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Prism Foil Settings)]
        _PrismScale ("Foil Pattern Scale", Float) = 30.0
        _PrismSpeed ("Shimmer Speed", Float) = 1.5
        _PrismIntensity ("Brightness", Range(0.5, 3.0)) = 1.5
        [Toggle] _UseDiamondPattern ("Use Diamond Pattern", Float) = 1.0

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
            fixed4 _Color;
            float4 _ClipRect;
            
            float _PrismScale;
            float _PrismSpeed;
            float _PrismIntensity;
            float _UseDiamondPattern;

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
                // 1. 홀로그램 그리드 및 셀(Cell) 계산
                float2 foilUV = IN.texcoord * _PrismScale;
                float2 cell = floor(foilUV);
                float2 local = frac(foilUV);

                // 2. 각 셀마다 무작위 오프셋 생성 (반짝임이 불규칙하게 보이도록)
                float hash = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);

                // 3. 빛 반사 각도 시뮬레이션 (시간, UV 좌표, Hash 값을 결합)
                float phase = _Time.y * _PrismSpeed + (IN.texcoord.x + IN.texcoord.y) * 2.0 + hash * 1.5;

                // 4. 무지개 빛깔 생성 (Cosine Palette 방식)
                fixed3 rainbow = 0.5 + 0.5 * cos(phase + fixed3(0.0, 2.094, 4.188)); // RGB 위상을 120도씩 분리
                rainbow = pow(rainbow, 1.5); // 메탈릭한 느낌을 위해 대비 증가

                // 5. 호일 특유의 흰색 하이라이트(빛 맺힘) 추가
                float highlight = pow(max(0, cos(phase * 2.0)), 4.0) * 0.4;
                rainbow += highlight;
                rainbow *= _PrismIntensity;

                // 6. 패턴 마스크 적용 (다이아몬드/별 모양 vs 기본 사각형)
                float diamondDist = abs(local.x - 0.5) + abs(local.y - 0.5);
                float diamondMask = smoothstep(0.55, 0.45, diamondDist);
                float shape = lerp(1.0, diamondMask, _UseDiamondPattern);

                fixed3 foilColor = rainbow * shape;

                // 7. 캐릭터(Sprite)와 호일 배경 합성
                // 캐릭터의 픽셀 불투명도(Alpha)를 기준으로 덮어씌웁니다.
                fixed4 spriteColor = tex2D(_MainTex, IN.texcoord) * IN.color;
                
                fixed4 finalColor;
                finalColor.rgb = lerp(foilColor, spriteColor.rgb, spriteColor.a);
                finalColor.a = IN.color.a; // CanvasGroup 등의 전체 알파 페이드 유지

                // Unity UI Rect Mask 2D 클리핑 처리
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