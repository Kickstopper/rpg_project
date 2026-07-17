Shader "Custom/UnderwaterDistortion"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        
        // 일렁임 강도를 제어하는 변수 (0이면 일렁임 없음, 0보다 크면 일렁임)
        _WaveAmount ("Wave Amount", Float) = 0.0
        _WaveSpeed ("Wave Speed", Float) = 2.0
        _WaveFrequency ("Wave Frequency", Float) = 10.0
        
        // 물속 푸른빛 틴트
        _WaterColor ("Water Color", Color) = (0.2, 0.5, 0.8, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
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
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _Color;
            float _WaveAmount;
            float _WaveSpeed;
            float _WaveFrequency;
            float4 _WaterColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // =========================================================
                // UV 좌표를 시간에 따라 사인(Sine) 곡선으로 왜곡시킵니다.
                // _WaveAmount가 0이면 이 연산은 uv 좌표에 아무 영향도 주지 않습니다.
                // =========================================================
                float2 distortedUV = i.uv;
                distortedUV.x += sin(distortedUV.y * _WaveFrequency + _Time.y * _WaveSpeed) * _WaveAmount;
                distortedUV.y += cos(distortedUV.x * _WaveFrequency + _Time.y * _WaveSpeed) * (_WaveAmount * 0.5);

                // 왜곡된 UV로 던전 텍스처를 읽어옵니다.
                fixed4 col = tex2D(_MainTex, distortedUV) * i.color;

                // 물속이라면 텍스처 색상에 약간의 푸른빛(WaterColor)을 섞어줍니다.
                if (_WaveAmount > 0)
                {
                    col.rgb = lerp(col.rgb, _WaterColor.rgb, 0.15); // 15% 정도 푸른빛 틴트 적용
                }

                return col;
            }
            ENDCG
        }
    }
}