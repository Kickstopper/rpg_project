Shader "Custom/PaletteCycling"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _PaletteTex ("Palette Texture (Wrap: Repeat)", 2D) = "white" {}
        _CycleSpeed ("Cycle Speed", Float) = 1.0
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"
            
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
                float2 texcoord  : TEXCOORD0;
            };
            
            fixed4 _Color;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap (OUT.vertex);
                #endif

                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _PaletteTex;
            float _CycleSpeed;

            fixed4 frag(v2f IN) : SV_Target
            {
                // 메인 스프라이트 텍스처에서 컬러 추출
                fixed4 texColor = tex2D(_MainTex, IN.texcoord);
                
                // 알파 값이 0이면 연산 생략 (성능 최적화 및 투명도 유지)
                if (texColor.a == 0.0)
                    return fixed4(0, 0, 0, 0);

                // 메인 텍스처의 R 채널을 팔레트 조회를 위한 인덱스(U 좌표)로 사용
                // _Time.y(시간)와 속도를 곱해 가로로 계속 밀어줍니다.
                float paletteLookup = texColor.r + (_Time.y * _CycleSpeed);
                
                // 팔레트 텍스처에서 새로운 색상 샘플링 (1D 형태이므로 V축은 0.5로 고정)
                fixed4 finalColor = tex2D(_PaletteTex, float2(paletteLookup, 0.5));
                
                // 기존 스프라이트의 Tint 컬러 및 원본 알파 값 적용
                finalColor.rgb *= IN.color.rgb;
                finalColor.a *= texColor.a;
                
                // 스프라이트 셰이더 특성인 프리멀티플라이드 알파(Premultiplied Alpha) 처리
                finalColor.rgb *= finalColor.a;

                return finalColor;
            }
        ENDCG
        }
    }
}