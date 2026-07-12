Shader "Custom/UI/RetroPrismFoil_CustomPattern"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Prism Foil Settings)]
        _PatternTex ("Pattern Mask Texture", 2D) = "white" {} // 임의의 도형 패턴을 받을 텍스처
        _PrismScale ("Foil Pattern Scale", Float) = 30.0
        _PrismSpeed ("Shimmer Speed", Float) = 1.5
        _PrismIntensity ("Brightness", Range(0.5, 3.0)) = 1.5

        // Unity UI Masking & Stencil
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
            sampler2D _PatternTex; // 패턴 텍스처 샘플러 추가
            fixed4 _Color;
            float4 _ClipRect;
            
            float _PrismScale;
            float _PrismSpeed;
            float _PrismIntensity;

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
                float2 cell = floor(foilUV);
                
                // 각 셀마다 무작위 오프셋 생성
                float hash = frac(sin(dot(cell, float2(12.9898, 78.233))) * 43758.5453);

                // 빛 반사 각도 시뮬레이션
                float phase = _Time.y * _PrismSpeed + (IN.texcoord.x + IN.texcoord.y) * 2.0 + hash * 1.5;

                // 무지개 빛깔 생성
                fixed3 rainbow = 0.5 + 0.5 * cos(phase + fixed3(0.0, 2.094, 4.188));
                rainbow = pow(rainbow, 1.5);

                // 하이라이트 추가
                float highlight = pow(max(0, cos(phase * 2.0)), 4.0) * 0.4;
                rainbow += highlight;
                rainbow *= _PrismIntensity;

                // --- [변경점] 수학적 계산 대신 텍스처의 알파 채널을 마스크로 샘플링 ---
                // foilUV를 그대로 사용하면 스케일에 맞춰 텍스처가 자동으로 타일링(반복)됩니다.
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