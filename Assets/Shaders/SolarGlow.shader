Shader "SolarSystem/Glow"
{
    // Camera-facing additive billboard with a procedural radial falloff (sun corona / halo).
    Properties
    {
        [HDR] _Color("Color (HDR)", Color) = (1.0, 0.6, 0.25, 1)
        _Power("Falloff Power", Float) = 3
        _Core("Core Size", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Glow"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Power;
                float _Core;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 center = TransformObjectToWorld(float3(0, 0, 0));
                float sx = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float sy = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 posWS = center + right * IN.positionOS.x * sx + up * IN.positionOS.y * sy;
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float r = length(IN.uv - 0.5) * 2.0;
                float g = pow(saturate(1.0 - r), _Power);
                g += saturate(1.0 - r / max(_Core, 0.001)) * 0.8;
                return half4(_Color.rgb * g, 1);
            }
            ENDHLSL
        }
    }
}
