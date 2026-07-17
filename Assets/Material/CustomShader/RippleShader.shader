Shader "Custom/RippleShader"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Center ("Ripple Center", Vector) = (0.5,0.5,0,0)
        _Speed ("Speed", Range(1, 10)) = 2.0
        _Amount ("Amount", Range(0.0, 1.0)) = 0.1
        _Frequency ("Frequency", Range(0.0, 10.0)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        CGPROGRAM
        #pragma surface surf Lambert

        sampler2D _MainTex;
        float4 _Center;
        float _Speed;
        float _Amount;
        float _Frequency;

        struct Input
        {
            float2 uv_MainTex;
        };

        void surf (Input IN, inout SurfaceOutput o)
        {
            float2 uv = IN.uv_MainTex;
            float dist = distance(uv, _Center.xy);
            uv += sin(dist * _Frequency - _Time.y * _Speed) * _Amount;
            fixed4 c = tex2D(_MainTex, uv);
            o.Albedo = c.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
