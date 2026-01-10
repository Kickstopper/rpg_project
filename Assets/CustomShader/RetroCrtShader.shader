Shader "UI/RetroCRTShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        [Header(Scanline Settings)]
        _ScanlineSize ("Scanline Count", Float) = 100.0
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.5
        
        [Header(Dot Matrix Settings)]
        _DotSize ("Dot Matrix Size", Float) = 50.0
        _DotIntensity ("Dot Matrix Intensity", Range(0, 1)) = 0.0

        [Header(Atmosphere)]
        _Brightness ("Brightness Boost", Range(1, 2)) = 1.2
        _VignetteSize ("Vignette Size", Range(0.1, 2.0)) = 1.5
        _VignetteSmooth ("Vignette Smoothness", Range(0.1, 1.0)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Transparent" }
        LOD 100
        
        // UI 요소와 잘 어우러지도록 블렌딩 설정
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            
            float _ScanlineSize;
            float _ScanlineIntensity;
            float _DotSize;
            float _DotIntensity;
            float _Brightness;
            float _VignetteSize;
            float _VignetteSmooth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 1. 기본 텍스처 색상 가져오기
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // 2. 스캔라인 (가로 줄무늬)
                // UV Y좌표에 사인파를 적용하여 밝고 어두운 줄무늬 생성
                float scanline = sin(i.uv.y * _ScanlineSize * 3.14159 * 2.0);
                // -1~1 범위를 0~1로 보정하고 강도 적용
                float scanLineEffect = lerp(1.0, 0.5 + 0.5 * scanline, _ScanlineIntensity);
                
                // 3. 도트 매트릭스 (격자 무늬)
                // UV X, Y 모두에 사인파 적용
                float dotX = sin(i.uv.x * _DotSize * 3.14159 * 2.0);
                float dotY = sin(i.uv.y * _DotSize * 3.14159 * 2.0);
                float dotPattern = dotX * dotY;
                float dotEffect = lerp(1.0, 0.5 + 0.5 * dotPattern, _DotIntensity);

                // 4. 비네팅 (화면 가장자리 어둡게)
                // 중앙(0.5, 0.5)에서의 거리 계산
                float2 dist = i.uv - 0.5;
                float len = length(dist);
                float vignette = smoothstep(_VignetteSize, _VignetteSize - _VignetteSmooth, len);

                // 5. 효과 합성
                col.rgb *= scanLineEffect;
                col.rgb *= dotEffect;
                col.rgb *= vignette;
                
                // 스캔라인 등으로 어두워진 화면 보정
                col.rgb *= _Brightness;

                return col;
            }
            ENDCG
        }
    }
}