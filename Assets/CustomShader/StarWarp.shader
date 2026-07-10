Shader "Custom/StarWarp"
{
    Properties
    {
        _Speed ("Speed", Range(0.0, 20.0)) = 5.0 // 이동 속도
        _Density ("Density", Range(10, 100)) = 40.0 // 별 밀도
        _Stretch ("Stretch", Range(0.0, 10.0)) = 3.0 // 선 늘어남 정도
    }
    SubShader
    {
        // Additive 블렌딩을 사용하여 배경을 투명하게 하고 별들이 빛나게 만듦
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend One One 
        ZWrite Off
        
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

            float _Speed;
            float _Density;
            float _Stretch;

            // 난수 생성 함수 (별의 위치와 크기를 랜덤하게 만듦)
            float random(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453123);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 하나의 별 레이어를 그리는 함수
            float3 getStarLayer(float2 uv, float angle, float depth, float density, float speedMultiplier, float seed)
            {
                float3 color = float3(0, 0, 0);
                
                // 속도가 빠를 때 선으로 보이도록 여러 번 샘플링 (잔상 효과)
                int samples = 15; 

                for (int j = 0; j < samples; j++)
                {
                    // 샘플마다 시간을 살짝 늦춰서 뒤로 꼬리가 생기도록 유도
                    float t = _Time.y * _Speed * speedMultiplier - (float)j * _Stretch * 0.01;
                    
                    // Z축(깊이) 이동
                    float z = depth + t + seed * 100.0;

                    // 극좌표계를 이용해 화면을 그리드(Grid)로 분할
                    float cellX = floor((angle / 6.2831853) * density);
                    float cellY = floor(z * density * 0.1);

                    // 각도가 한 바퀴 돌 때 경계선이 생기지 않도록 보정
                    cellX = fmod(cellX, density);
                    if(cellX < 0.0) cellX += density;

                    // 해당 그리드 셀의 고유 랜덤값
                    float rand = random(float2(cellX, cellY));

                    // 그리드 셀 내부의 로컬 좌표 (0.0 ~ 1.0)
                    float fX = frac((angle / 6.2831853) * density);
                    float fY = frac(z * density * 0.1);

                    // 셀 중심(0.5, 0.5)으로부터의 거리
                    float d = length(float2(fX - 0.5, fY - 0.5));

                    // 별 그리기 (rand 값이 0.98 이상인 아주 일부 셀에만 별을 생성)
                    float size = lerp(0.05, 0.15, frac(rand * 12.34)); 
                    float star = smoothstep(size, size * 0.5, d) * step(0.98, rand); 

                    // 별 색상 약간씩 다르게 (푸른빛 ~ 노란빛)
                    float3 starCol = lerp(float3(0.5, 0.8, 1.0), float3(1.0, 0.9, 0.6), frac(rand * 56.78));

                    // 중심부(무한대)에서 갑자기 튀어나오지 않고, 외곽에서 자연스럽게 사라지도록 페이드 처리
                    float fade = smoothstep(1.0, 3.0, depth) * (1.0 - smoothstep(10.0, 30.0, depth));

                    // max를 사용하여 겹치는 별빛(잔상)이 밝기를 유지하며 하나로 이어지게 함
                    color = max(color, star * starCol * fade);
                }
                return color;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // UV 좌표를 화면 중앙(0,0)으로 이동
                float2 uv = i.uv - 0.5;
                
                // 극좌표계 계산 (중심으로부터의 거리와 각도)
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);
                
                // 원근감을 위한 깊이(Depth) 계산 (거리가 가까울수록 무한대에 가까워짐)
                float depth = 1.0 / max(dist, 0.0001);
                
                float3 finalColor = float3(0, 0, 0);
                
                // 깊이감을 주기 위해 속도와 밀도가 다른 2개의 레이어를 겹침
                // 배경 레이어 (작고 촘촘하고 느림)
                finalColor += getStarLayer(uv, angle, depth, _Density * 1.5, 0.5, 1.0) * 0.5;
                // 전경 레이어 (크고 듬성듬성하고 빠름)
                finalColor += getStarLayer(uv, angle, depth, _Density, 1.0, 2.0);

                return fixed4(finalColor, 1.0);
            }
            ENDCG
        }
    }
}