Shader "Custom/Pseudo3DFloor"
{
    Properties
    {
        _MainTex ("Grid Texture (PNG)", 2D) = "white" {}
        _Speed ("Scroll Speed", Float) = 5.0
        _Fov ("Field of View (Width)", Float) = 1.5
        _Height ("Camera Height", Float) = 1.0
        
        [Header(Horizon Fade)]
        _HorizonColor ("Horizon Fade Color", Color) = (0,0,0,0)
        _FadeStart ("Fade Start Distance", Float) = 2.0
        _FadeEnd ("Fade End Distance", Float) = 20.0
    }
    SubShader
    {
        // PNG의 투명도를 지원하기 위한 태그 및 블렌딩 설정
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
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

            sampler2D _MainTex;
            float _Speed;
            float _Fov;
            float _Height;
            float4 _HorizonColor;
            float _FadeStart;
            float _FadeEnd;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; 
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float horizonY = 1.0 - i.uv.y;
                
                horizonY = max(horizonY, 0.001); 

                float depth = _Height / horizonY;

                float2 pseudoUV;
                
                // X축 왜곡. 깊이가 깊어질수록 더 넓은 영역의 텍스처를 사다리꼴로 왜곡해서 보여줌
                pseudoUV.x = (i.uv.x - 0.5) * depth * _Fov + 0.5;

                // Y축 왜곡 및 스크롤. 시간에 따라 앞으로 전진하는 효과
                pseudoUV.y = depth - _Time.y * _Speed;

                // 왜곡된 UV로 텍스처를 샘플링
                fixed4 col = tex2D(_MainTex, pseudoUV);

                // 수평선 근처에서 텍스처가 뭉개지는 아티팩트를 숨기기 위한 페이드 아웃 연산
                float fade = clamp((depth - _FadeStart) / (_FadeEnd - _FadeStart), 0.0, 1.0);
                
                // 텍스처의 색상과 알파값을 Horizon Color로 부드럽게 전환
                col.rgb = lerp(col.rgb, _HorizonColor.rgb, fade);
                col.a = lerp(col.a, _HorizonColor.a, fade);

                return col;
            }
            ENDCG
        }
    }
}