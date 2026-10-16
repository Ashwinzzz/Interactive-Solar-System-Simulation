Shader "SolarSystem/Ring"
{
    // Planetary ring: u = radial position (0 inner .. 1 outer). Receives the planet's shadow.
    Properties
    {
        _BaseMap("Ring Strip", 2D) = "white" {}
        _Opacity("Opacity", Range(0, 1)) = 1
        _Ambient("Ambient", Range(0, 0.3)) = 0.03
        _PlanetCenter("Planet Center", Vector) = (0, 0, 0, 0)
        _PlanetRadius("Planet Radius", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Ring"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Opacity;
                float _Ambient;
                float4 _PlanetCenter;
                float _PlanetRadius;
            CBUFFER_END

            float4 _SunPos;
            float4 _SunParams;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 strip = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(IN.uv.x, 0.5));
                float3 toSun = _SunPos.xyz - IN.positionWS;
                float dist = length(toSun);
                float3 L = toSun / max(dist, 0.0001);
                float atten = _SunParams.x / (1.0 + dist / max(_SunParams.y, 0.01));

                // planet shadow cast on the ring
                float3 rel = IN.positionWS - _PlanetCenter.xyz;
                float b = dot(rel, L);
                float dmin = sqrt(max(0.0, dot(rel, rel) - b * b));
                float shadow = (b < 0.0) ? (1.0 - smoothstep(_PlanetRadius * 0.93, _PlanetRadius * 1.03, dmin)) : 0.0;

                float light = (1.0 - shadow * 0.93) * atten + _Ambient;
                return half4(strip.rgb * light, strip.a * _Opacity);
            }
            ENDHLSL
        }
    }
}
