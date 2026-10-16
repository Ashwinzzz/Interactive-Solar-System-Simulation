Shader "SolarSystem/Sun"
{
    Properties
    {
        _BaseMap("Surface", 2D) = "white" {}
        [HDR] _Color("Color (HDR)", Color) = (4, 2.1, 0.8, 1)
        _FlowSpeed("Surface Flow Speed", Float) = 0.004
        _LimbDark("Limb Darkening", Range(0, 1)) = 0.55
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Color;
                float _FlowSpeed;
                float _LimbDark;
            CBUFFER_END

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
                float t = _Time.y * _FlowSpeed;
                float3 a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv + float2(t, 0)).rgb;
                float3 b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(IN.uv.x * 2.0 - t * 1.3, IN.uv.y)).rgb;
                float3 tex = lerp(a, b, 0.5);
                float mu = saturate(dot(normalize(IN.normalWS), normalize(GetCameraPositionWS() - IN.positionWS)));
                float limb = lerp(1.0 - _LimbDark, 1.0, pow(mu, 0.45));
                float3 limbTint = lerp(float3(1.0, 0.45, 0.18), float3(1, 1, 1), pow(mu, 0.4));
                float3 col = tex * _Color.rgb * limb * limbTint * (0.8 + 0.4 * tex.r);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
