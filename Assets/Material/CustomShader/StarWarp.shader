Shader "Custom/StarWarp"
{
    Properties
    {
        _Density ("Density", Range(10, 100)) = 40.0
        _Stretch ("Stretch", Range(0.0, 10.0)) = 3.0
        
        _StarSizeMin ("Star Size Min", Float) = 0.05
        _StarSizeMax ("Star Size Max", Float) = 0.35
        _ViewOffset ("View Offset", Vector) = (0, 0, 0, 0)
        
        _CustomTime ("Custom Time", Float) = 0.0
        _MasterAlpha ("Master Alpha", Range(0.0, 1.0)) = 1.0
    }
    SubShader
    {
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

            float _Density;
            float _Stretch;
            float _StarSizeMin;
            float _StarSizeMax;
            float4 _ViewOffset;
            
            float _CustomTime;
            float _MasterAlpha;

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

            float3 getStarLayer(float2 uv, float angle, float depth, float density, float speedMultiplier, float seed)
            {
                float3 color = float3(0, 0, 0);
                
                // 샘플 수를 30으로 늘려 더 촘촘한 간격으로 잔상 생성
                int samples = 30; 
                
                // 점을 선형으로 메우기 위한 모양 늘림(Stretch) 배수 계산
                // Stretch 수치에 비례해서 입자 모양 자체가 길어짐
                float shapeStretch = max(1.0, _Stretch * 2.0); 

                for (int j = 0; j < samples; j++)
                {
                    // 간격(0.01 -> 0.005)을 절반으로 줄여 전체 꼬리 길이는 유지하되 밀도를 높임
                    float t = _CustomTime * speedMultiplier - (float)j * _Stretch * 0.005;
                    float z = depth + t + seed * 100.0;

                    float cellX = floor((angle / 6.2831853) * density);
                    float cellY = floor(z * density * 0.1);

                    cellX = fmod(cellX, density);
                    if(cellX < 0.0) cellX += density;

                    float rand = random(float2(cellX, cellY));
                    float fX = frac((angle / 6.2831853) * density);
                    float fY = frac(z * density * 0.1);

                    // (fY - 0.5)를 shapeStretch로 나누어줌
                    // 원형이던 점이 Z축(깊이) 방향으로 길쭉하게 늘어나며 완벽한 직선을 형성
                    float d = length(float2(fX - 0.5, (fY - 0.5) / shapeStretch));

                    float randSize = frac(rand * 12.34);
                    float size = lerp(_StarSizeMin, _StarSizeMax, pow(randSize, 3.0)); 
                    
                    // 별 경계선을 그림
                    float star = smoothstep(size, size * 0.5, d) * step(0.98, rand); 

                    float brightness = lerp(0.3, 1.5, randSize);
                    float3 starCol = lerp(float3(0.5, 0.8, 1.0), float3(1.0, 0.9, 0.6), frac(rand * 56.78)) * brightness;

                    float fade = smoothstep(1.0, 3.0, depth) * (1.0 - smoothstep(10.0, 30.0, depth));
                    color = max(color, star * starCol * fade);
                }
                return color;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5 + _ViewOffset.xy;
                
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);
                float depth = 1.0 / max(dist, 0.0001);
                
                float3 finalColor = float3(0, 0, 0);
                
                finalColor += getStarLayer(uv, angle, depth, _Density * 1.5, 0.5, 1.0) * 0.5;
                finalColor += getStarLayer(uv, angle, depth, _Density, 1.0, 2.0);

                return fixed4(finalColor * _MasterAlpha, 1.0);
            }
            ENDCG
        }
    }
}