Shader "SolarSystem/StarSky"
{
    // Procedural star field: stars are evaluated per pixel in direction space, so they stay
    // crisp and sub-pixel at any zoom. A latitude/longitude texture supplies the Milky Way dust band.
    Properties
    {
        _MainTex("Milky Way (lat/long)", 2D) = "black" {}
        _MilkyWay("Milky Way Intensity", Float) = 1.0
        _StarBrightness("Star Brightness", Float) = 1.0
        _Exposure("Exposure", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "StarSky"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _MilkyWay;
                float _StarBrightness;
                float _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            float3 Hash33(float3 p3)
            {
                p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return frac((p3.xxy + p3.yxx) * p3.zyx);
            }

            float3 StarLayer(float3 d, float scale, float density, float sizeBase, float seed, float gain)
            {
                float3 p = d * scale;
                float3 cell = floor(p);
                float3 f = p - cell;
                float h = Hash13(cell + seed);
                if (h > density) return float3(0, 0, 0);

                float3 pos = 0.28 + 0.44 * Hash33(cell + seed + 3.1);
                float dist = length(f - pos);
                float footprint = length(fwidth(p));
                float sigma = max(sizeBase, footprint * 0.55);
                float energy = 1.0;
                float r = Hash13(cell + seed + 7.7);
                float mag = 0.10 + pow(r, 5.0) * 3.2;
                float g = exp(-(dist * dist) / (2.0 * sigma * sigma)) * energy * mag * gain;

                float t = Hash13(cell + seed + 13.3);
                float3 col = t < 0.14 ? float3(0.62, 0.76, 1.0)
                           : t < 0.55 ? float3(1.0, 0.98, 0.95)
                           : t < 0.85 ? float3(1.0, 0.88, 0.70)
                           : float3(1.0, 0.68, 0.48);
                return col * g;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float2 uv = float2(frac(atan2(d.z, d.x) / (2.0 * PI) + 1.0), asin(clamp(d.y, -1.0, 1.0)) / PI + 0.5);
                float3 dust = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 1.5).rgb * _MilkyWay;

                float3 stars = StarLayer(d, 60.0, 0.14, 0.013, 1.0, 1.0)
                             + StarLayer(d, 130.0, 0.09, 0.012, 21.0, 0.9)
                             + StarLayer(d, 260.0, 0.06, 0.011, 57.0, 0.8)
                             + StarLayer(d, 520.0, 0.04, 0.010, 91.0, 0.7);

                // stars shine through the Milky Way more densely: boost near the band
                float bandBoost = 1.0 + dot(dust, float3(1, 1, 1)) * 3.0;
                float3 col = (dust + stars * _StarBrightness * bandBoost) * _Exposure;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
