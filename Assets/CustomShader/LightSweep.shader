Shader "UI/Custom/LightSweep"
{
    Properties
    {
        // UI 기본 프로퍼티
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // 빛 효과 세팅
        [Header(Shine Settings)]
        [HDR] _ShineColor ("Shine Color", Color) = (1, 1, 1, 0.5)
        _ShineWidth ("Shine Width", Range(0.01, 1.0)) = 0.1
        _ShineSoftness ("Shine Softness", Range(0.001, 1.0)) = 0.05
        _ShineSpeed ("Shine Speed", Float) = 1.0
        _ShineAngle ("Shine Angle", Range(0, 360)) = -45.0

        // 자연스러운 합성 세팅
        [Header(Natural Blending)]
        _ShineBlend ("Screen Blend (0=Add, 1=Screen)", Range(0, 1)) = 1.0
        _ShineTint ("Inherit Base Color", Range(0, 1)) = 0.4

        // UI 마스킹 지원을 위한 프로퍼티
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            
            float4 _ShineColor;
            float _ShineWidth;
            float _ShineSoftness;
            float _ShineSpeed;
            float _ShineAngle;
            
            float _ShineBlend;
            float _ShineTint;

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
                // 기본 UI 텍스처 및 색상
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                // 각도를 라디안으로 변환 후 UV 회전
                float rad = _ShineAngle * (UNITY_PI / 180.0);
                float s = sin(rad);
                float c = cos(rad);
                float2 centeredUV = IN.texcoord - 0.5;
                float rotatedY = centeredUV.x * s + centeredUV.y * c;

                // 시간 계산 (음수 Speed 완벽 지원)
                float currentPos = frac(_Time.y * _ShineSpeed) * 3.0 - 1.5;

                // 중심선으로부터의 거리를 계산해 빛의 두께와 부드러움 적용
                float dist = abs(rotatedY - currentPos);
                float shinePower = 1.0 - smoothstep(_ShineWidth - _ShineSoftness, _ShineWidth, dist);

                // 빛 합성
                // 원본 이미지의 색상을 흡수할지 여부 결정 (ShineTint) 색상을 2.0배 곱해 원본 색을 살리면서도 밝기를 잃지 않게 함
                half3 baseColorShine = color.rgb * _ShineColor.rgb * 2.0; 
                half3 tintedShineColor = lerp(_ShineColor.rgb, baseColorShine, _ShineTint);
                
                // 최종 빛의 강도와 투명도 적용
                half3 finalShine = tintedShineColor * (_ShineColor.a * shinePower * color.a);

                // 두 가지 합성 방식 계산
                half3 additive = color.rgb + finalShine; // 강렬한 빛
                half3 screen = color.rgb + finalShine - (color.rgb * finalShine); // 부드러운 빛
                
                // 슬라이더 값에 따라 두 방식을 부드럽게 섞기
                color.rgb = lerp(additive, screen, _ShineBlend);

                // UI 마스크 자르기 기능 적용
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}