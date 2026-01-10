Shader "Custom/UI_HeatHaze"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 아지랑이 설정값
        _DistortionStrength ("Strength (강도)", Range(0, 0.1)) = 0.005
        _Frequency ("Frequency (물결 빈도)", Range(0, 50)) = 15
        _Speed ("Speed (속도)", Range(0, 5)) = 2.0
        
        // UI 필수 속성 (마스크 처리를 위해 필요)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _WriteMask ("Stencil Write Mask", Float) = 255
        _ReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
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
            ReadMask [_ReadMask]
            WriteMask [_WriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_ST;
            
            // 변수 선언
            float _DistortionStrength;
            float _Frequency;
            float _Speed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // [핵심 로직] 아지랑이 효과 계산
                // Y좌표(높이)와 시간(_Time)을 이용해 사인파(Sine Wave)를 만듭니다.
                // 이 값을 X좌표(좌우)에 더해서 픽셀을 옆으로 살짝 밉니다.
                
                float wave = sin(i.texcoord.y * _Frequency - _Time.y * _Speed);
                float distortion = wave * _DistortionStrength;

                // 왜곡된 UV 좌표
                float2 distortedUV = i.texcoord;
                distortedUV.x += distortion;

                // 텍스처 샘플링 (왜곡된 위치의 색을 가져옴)
                half4 color = tex2D(_MainTex, distortedUV) * i.color;
                
                // UI 마스크 처리 (Clip)
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return color;
            }
            ENDCG
        }
    }
}