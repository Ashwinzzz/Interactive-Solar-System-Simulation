Shader "SolarSystem/Clouds"
{
    Properties
    {
        _BaseMap("Cloud Density", 2D) = "black" {}
        _Opacity("Opacity", Range(0, 1)) = 1
        _Ambient("Ambient", Range(0, 0.3)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-1" }
        Pass
        {
            Name "Clouds"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _Opacity;
                float _Ambient;
            CBUFFER_END

            float4 _SunPos;
            float4 _SunParams;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float density = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).r;
                float3 N = normalize(IN.normalWS);
                float3 toSun = _SunPos.xyz - IN.positionWS;
                float dist = length(toSun);
                float3 L = toSun / max(dist, 0.0001);
                float atten = _SunParams.x / (1.0 + dist / max(_SunParams.y, 0.01));
                float ndl = dot(N, L);
                float light = smoothstep(-0.05, 0.25, ndl) * saturate(ndl * 0.8 + 0.3) * atten + _Ambient;
                // warm tint near the terminator (sunset scattering)
                float3 tint = lerp(float3(1.0, 0.62, 0.40), float3(1, 1, 1), smoothstep(0.0, 0.35, ndl));
                return half4(tint * light, density * _Opacity);
            }
            ENDHLSL
        }
    }
}
