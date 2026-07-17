Shader "UI/RetroCRTShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        
        [Header(CRT Curvature)]
        _Curvature ("Curvature Amount (0 is flat)", Range(0.0, 1.0)) = 0.2
        _BlurAmount ("Curve Smoothing (Blur)", Range(0.0, 0.01)) = 0.0015
        
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
            
            float _Curvature;
            float _BlurAmount;
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

            // 곡률 계산
            float2 DistortUV(float2 uv, float curvature)
            {
                // 0~1의 UV를 -1~1 범위로 변환하여 중앙을 영점으로 맞춤
                uv = uv * 2.0 - 1.0;
                
                // curvature를 곱하여 왜곡 적용
                uv += uv * (uv.yx * uv.yx) * curvature;
                
                // 다시 0~1 범위로 복구
                return uv * 0.5 + 0.5;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UV에 곡률 적용
                float2 curvedUV = DistortUV(i.uv, _Curvature);

                // 2. 곡률로 인해 0~1 범위를 벗어난 가장자리 픽셀을 투명(또는 검은색) 처리
                if (curvedUV.x < 0.0 || curvedUV.x > 1.0 || curvedUV.y < 0.0 || curvedUV.y > 1.0)
                {
                    return fixed4(0, 0, 0, 0); 
                }

                // 3. 텍스처 색상 가져오기 및 조건부 블러 처리
                fixed4 col = fixed4(0, 0, 0, 0);

                if (_Curvature > 0.001) 
                {
                    // 곡률이 활성화되었을 때: 십자(+) 형태로 5번 샘플링하여 뭉개주기
                    fixed4 c1 = tex2D(_MainTex, curvedUV);
                    fixed4 c2 = tex2D(_MainTex, curvedUV + float2(_BlurAmount, 0));
                    fixed4 c3 = tex2D(_MainTex, curvedUV + float2(-_BlurAmount, 0));
                    fixed4 c4 = tex2D(_MainTex, curvedUV + float2(0, _BlurAmount));
                    fixed4 c5 = tex2D(_MainTex, curvedUV + float2(0, -_BlurAmount));

                    // // 각각 CGA 팔레트로 스냅
                    // c1.rgb = ApplyCGAPalette(c1.rgb);
                    // c2.rgb = ApplyCGAPalette(c2.rgb);
                    // c3.rgb = ApplyCGAPalette(c3.rgb);
                    // c4.rgb = ApplyCGAPalette(c4.rgb);
                    // c5.rgb = ApplyCGAPalette(c5.rgb);

                    // 평균을 내어 부드럽게 합성
                    col = (c1 + c2 + c3 + c4 + c5) / 5.0;
                }
                else 
                {
                    col = tex2D(_MainTex, curvedUV);
                    //col.rgb = ApplyCGAPalette(col.rgb);
                }

                // 스캔라인
                float scanline = sin(curvedUV.y * _ScanlineSize * 3.14159 * 2.0);
                float scanLineEffect = lerp(1.0, 0.5 + 0.5 * scanline, _ScanlineIntensity);

                // 도트 매트릭스
                float dotX = sin(curvedUV.x * _DotSize * 3.14159 * 2.0);
                float dotY = sin(curvedUV.y * _DotSize * 3.14159 * 2.0);
                float dotPattern = dotX * dotY;
                float dotEffect = lerp(1.0, 0.5 + 0.5 * dotPattern, _DotIntensity);

                // 비네팅
                float2 dist = curvedUV - 0.5;
                float len = length(dist);
                float vignette = smoothstep(_VignetteSize, _VignetteSize - _VignetteSmooth, len);

                // 효과 합성
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