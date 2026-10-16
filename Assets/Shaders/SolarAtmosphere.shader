Shader "SolarSystem/Atmosphere"
{
    // Outer halo shell: render back faces additively so light hugs the planet limb and fades outward.
    Properties
    {
        [HDR] _Color("Color", Color) = (0.35, 0.6, 1.0, 1)
        _Power("Falloff Power", Float) = 2.5
        _Intensity("Intensity", Float) = 1.2
        _Sharp("Edge Sharpness", Float) = 2.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Atmosphere"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Power;
                float _Intensity;
                float _Sharp;
            CBUFFER_END

            float4 _SunPos;
            float4 _SunParams;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 toSun = _SunPos.xyz - IN.positionWS;
                float dist = length(toSun);
                float3 L = toSun / max(dist, 0.0001);
                float atten = _SunParams.x / (1.0 + dist / max(_SunParams.y, 0.01));
                // N points away from the viewer on back faces, so -dot(N,V) is the view-facing term
                float f = pow(saturate(-dot(N, V) * _Sharp), _Power);
                float lit = saturate(dot(N, L) * 0.9 + 0.35);
                return half4(_Color.rgb * f * _Intensity * lit * atten, 1);
            }
            ENDHLSL
        }
    }
}
