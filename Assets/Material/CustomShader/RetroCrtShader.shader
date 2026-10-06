Shader "UI/RetroCRTShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        [Header(CRT Curvature)]
        _Curvature ("Curvature Amount (0 is flat)", Range(0.0, 1.0)) = 0.010
        _BlurAmount ("CRT Beam Blur", Range(0.0, 0.01)) = 0.00075

        [Header(Chromatic Aberration)]
        _ChromaticAberration ("RGB Separation", Range(0.0, 0.005)) = 0.00070
        _ChromaticEdgeBoost ("Edge Separation Boost", Range(0.0, 2.0)) = 0.65
        _ChromaticIntensity ("Chromatic Intensity", Range(0.0, 1.0)) = 0.50
        _ChromaticVertical ("Vertical Convergence", Range(0.0, 0.5)) = 0.04

        [Header(Scanline Beam)]
        _ScanlineSize ("Scanline Count", Float) = 128.0
        _ScanlineIntensity ("Scanline Intensity", Range(0.0, 1.0)) = 0.22
        _BeamSharpness ("Beam Sharpness", Range(0.5, 4.0)) = 1.5

        [Header(Phosphor Mask)]
        _PhosphorScale ("Phosphor Scale", Range(100.0, 1500.0)) = 720.0
        _PhosphorIntensity ("Phosphor Mask Intensity", Range(0.0, 0.5)) = 0.10

        [Header(CRT Glow)]
        _GlowAmount ("Glow Radius", Range(0.0, 0.005)) = 0.0012
        _GlowIntensity ("Glow Intensity", Range(0.0, 1.0)) = 0.10
        _GlowThreshold ("Glow Threshold", Range(0.0, 1.0)) = 0.70

        [Header(Atmosphere)]
        _Brightness ("Brightness Boost", Range(0.5, 2.0)) = 1.30
        _VignetteSize ("Vignette Size", Range(0.1, 2.0)) = 0.92
        _VignetteSmooth ("Vignette Smoothness", Range(0.1, 1.0)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent"
        }

        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            #define CRT_PI 3.14159265359
            #define CRT_TAU 6.28318530718

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

            float _ChromaticAberration;
            float _ChromaticEdgeBoost;
            float _ChromaticIntensity;
            float _ChromaticVertical;

            float _ScanlineSize;
            float _ScanlineIntensity;
            float _BeamSharpness;

            float _PhosphorScale;
            float _PhosphorIntensity;

            float _GlowAmount;
            float _GlowIntensity;
            float _GlowThreshold;

            float _Brightness;
            float _VignetteSize;
            float _VignetteSmooth;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // ------------------------------------------------------------
            // CRT 곡률
            // ------------------------------------------------------------
            float2 DistortUV(float2 uv, float curvature)
            {
                uv = uv * 2.0 - 1.0;
                uv += uv * (uv.yx * uv.yx) * curvature;
                return uv * 0.5 + 0.5;
            }

            // ------------------------------------------------------------
            // 빔 블러
            // CRT 빔은 수직 확산보다 수평 확산을 약간 더 강하게 주었을 때 가독성이 더 좋음
            // ------------------------------------------------------------
            fixed4 SampleBeamBlur(float2 uv)
            {
                if (_BlurAmount <= 0.000001)
                    return tex2D(_MainTex, uv);

                fixed4 center = tex2D(_MainTex, uv);
                fixed4 left   = tex2D(_MainTex, saturate(uv + float2(-_BlurAmount, 0.0)));
                fixed4 right  = tex2D(_MainTex, saturate(uv + float2( _BlurAmount, 0.0)));
                fixed4 up     = tex2D(_MainTex, saturate(uv + float2(0.0,  _BlurAmount)));
                fixed4 down   = tex2D(_MainTex, saturate(uv + float2(0.0, -_BlurAmount)));

                return
                    center * 0.40 +
                    (left + right) * 0.18 +
                    (up + down) * 0.12;
            }

            // ------------------------------------------------------------
            // 가장자리 가중치가 적용된 RGB 수렴 오류.
            // 녹색 채널이 기준 채널로 유지됨. 적청 채널은 서로 반대 방향으로 벗어나며, 화면 가장자리로 갈수록 분리(색 테두리 현상)가 더 심해짐
            // ------------------------------------------------------------
            fixed3 ApplyChromaticConvergence(float2 uv, fixed3 baseColor)
            {
                if (_ChromaticAberration <= 0.000001 ||
                    _ChromaticIntensity <= 0.001)
                {
                    return baseColor;
                }

                float2 centered = uv - 0.5;
                float edgeDistance = saturate(length(centered) * 1.6);

                float separation =
                    _ChromaticAberration *
                    lerp(0.25, 1.0 + _ChromaticEdgeBoost, edgeDistance);

                float2 direction =
                    normalize(
                        float2(
                            centered.x,
                            centered.y * _ChromaticVertical
                        ) +
                        float2(0.00001, 0.0)
                    );

                float2 offset = direction * separation;

                float2 redUV  = saturate(uv + offset);
                float2 blueUV = saturate(uv - offset);

                fixed redSample  = tex2D(_MainTex, redUV).r;
                fixed blueSample = tex2D(_MainTex, blueUV).b;

                baseColor.r = lerp(baseColor.r, redSample,  _ChromaticIntensity);
                baseColor.b = lerp(baseColor.b, blueSample, _ChromaticIntensity);

                return baseColor;
            }

            // ------------------------------------------------------------
            // 밝기 의존형 글로우 / 할레이션.
            // 밝은 주변 텍셀만 강하게 기여하므로, 어두운 픽셀은 비교적 또렷하게 유지되는 반면 하이라이트는 살짝 번짐(블러).
            // ------------------------------------------------------------
            fixed3 SampleHighlightGlow(float2 uv)
            {
                if (_GlowAmount <= 0.000001 ||
                    _GlowIntensity <= 0.001)
                {
                    return fixed3(0.0, 0.0, 0.0);
                }

                fixed3 glow =
                    tex2D(_MainTex, saturate(uv + float2( _GlowAmount, 0.0))).rgb +
                    tex2D(_MainTex, saturate(uv + float2(-_GlowAmount, 0.0))).rgb +
                    tex2D(_MainTex, saturate(uv + float2(0.0,  _GlowAmount))).rgb +
                    tex2D(_MainTex, saturate(uv + float2(0.0, -_GlowAmount))).rgb;

                glow *= 0.25;

                float luminance =
                    dot(glow, float3(0.299, 0.587, 0.114));

                float thresholdRange =
                    max(0.001, 1.0 - _GlowThreshold);

                float glowMask =
                    saturate(
                        (luminance - _GlowThreshold) /
                        thresholdRange
                    );

                return glow * glowMask * _GlowIntensity;
            }

            // ------------------------------------------------------------
            // 소프트 CRT 빔 프로파일.
            // BeamSharpness는 발광하는 스캔라인 빔의 폭이 얼마나 좁아질지를 조절함.
            // intensity 속성은 전체적인 대비를 조절.
            // ------------------------------------------------------------
            float GetScanlineEffect(float2 uv)
            {
                float phase =
                    0.5 +
                    0.5 * sin(uv.y * _ScanlineSize * CRT_TAU);

                float shapedBeam =
                    pow(saturate(phase), _BeamSharpness);

                return lerp(
                    1.0,
                    shapedBeam,
                    _ScanlineIntensity
                );
            }

            // ------------------------------------------------------------
            // RGB 그릴 / 형광체 변조.
            // ------------------------------------------------------------
            float3 GetPhosphorMask(float2 uv)
            {
                if (_PhosphorIntensity <= 0.001)
                    return float3(1.0, 1.0, 1.0);

                float phase =
                    uv.x * _PhosphorScale * CRT_TAU;

                float3 phosphorWave =
                    0.5 +
                    0.5 * cos(
                        phase +
                        float3(
                            0.0,
                            CRT_TAU / 3.0,
                            CRT_TAU * 2.0 / 3.0
                        )
                    );

                float3 phosphorTarget =
                    0.68 + phosphorWave * 0.32;

                return lerp(
                    float3(1.0, 1.0, 1.0),
                    phosphorTarget,
                    _PhosphorIntensity
                );
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 curvedUV =
                    DistortUV(i.uv, _Curvature);

                if (curvedUV.x < 0.0 || curvedUV.x > 1.0 ||
                    curvedUV.y < 0.0 || curvedUV.y > 1.0)
                {
                    return fixed4(0.0, 0.0, 0.0, 0.0);
                }

                fixed4 col =
                    SampleBeamBlur(curvedUV);

                col.rgb =
                    ApplyChromaticConvergence(
                        curvedUV,
                        col.rgb
                    );

                col.rgb +=
                    SampleHighlightGlow(curvedUV);

                float scanlineEffect =
                    GetScanlineEffect(curvedUV);

                col.rgb *= scanlineEffect;

                float3 phosphorMask =
                    GetPhosphorMask(curvedUV);

                col.rgb *= phosphorMask;

                // 비네트
                float2 dist =
                    curvedUV - 0.5;

                float len =
                    length(dist);

                float vignette =
                    smoothstep(
                        _VignetteSize,
                        _VignetteSize - _VignetteSmooth,
                        len
                    );

                col.rgb *= vignette;

                // 스캔라인/마스크/비네트 효과로 인한 화면 어두워짐 보정
                col.rgb *= _Brightness;

                return col;
            }

            ENDCG
        }
    }
}
