Shader "SolarSystem/Planet"
{
    Properties
    {
        _BaseMap("Albedo", 2D) = "white" {}
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Float) = 1
        _SpecMap("Specular Mask", 2D) = "black" {}
        _NightMap("City Lights", 2D) = "black" {}
        _SpecPower("Specular Power", Float) = 80
        _SpecIntensity("Specular Intensity", Float) = 0.8
        _AtmoColor("Atmosphere Color", Color) = (0.4, 0.6, 1, 1)
        _AtmoPower("Atmosphere Falloff", Float) = 3
        _AtmoIntensity("Atmosphere Intensity", Float) = 0.8
        _Ambient("Ambient", Range(0, 0.3)) = 0.015
        _Brightness("Brightness", Float) = 1
        _NightIntensity("Night Light Intensity", Float) = 1.6
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

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
            TEXTURE2D(_SpecMap);  SAMPLER(sampler_SpecMap);
            TEXTURE2D(_NightMap); SAMPLER(sampler_NightMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _BumpScale;
                float _SpecPower;
                float _SpecIntensity;
                float4 _AtmoColor;
                float _AtmoPower;
                float _AtmoIntensity;
                float _Ambient;
                float _Brightness;
                float _NightIntensity;
            CBUFFER_END

            float4 _SunPos;     // xyz = world position of the Sun
            float4 _SunParams;  // x = brightness, y = falloff distance

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS;
                OUT.tangentWS = n.tangentWS;
                OUT.bitangentWS = n.bitangentWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv), _BumpScale);
                float3 Ng = normalize(IN.normalWS);
                float3 N = normalize(nTS.x * IN.tangentWS + nTS.y * IN.bitangentWS + nTS.z * Ng);
                float3 V = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 toSun = _SunPos.xyz - IN.positionWS;
                float dist = length(toSun);
                float3 L = toSun / max(dist, 0.0001);

                float atten = _SunParams.x / (1.0 + dist / max(_SunParams.y, 0.01));
                float ndlGeo = dot(Ng, L);
                float ndl = saturate(dot(N, L));
                // soften the terminator a little so the day/night edge looks like an atmosphere scatter
                float term = smoothstep(-0.02, 0.18, ndlGeo);
                float diffuse = ndl * term * atten;

                float3 col = albedo * (diffuse + _Ambient) * _Brightness;

                // ocean glint
                float spec = SAMPLE_TEXTURE2D(_SpecMap, sampler_SpecMap, IN.uv).r;
                float3 H = normalize(L + V);
                float s = pow(saturate(dot(N, H)), _SpecPower) * spec * _SpecIntensity * term * atten;
                col += s * float3(1.0, 0.96, 0.88);

                // city lights on the night side
                float night = 1.0 - smoothstep(-0.12, 0.10, ndlGeo);
                col += SAMPLE_TEXTURE2D(_NightMap, sampler_NightMap, IN.uv).rgb * night * _NightIntensity;

                // atmospheric limb glow
                float fres = pow(1.0 - saturate(dot(Ng, V)), _AtmoPower);
                float lit = saturate(ndlGeo * 1.6 + 0.22);
                col += _AtmoColor.rgb * fres * _AtmoIntensity * lit * atten;

                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
