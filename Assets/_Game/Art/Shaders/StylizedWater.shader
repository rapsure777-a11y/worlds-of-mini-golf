// Gamebreak stylized ocean / lagoon.
// No depth texture (disabled for VR performance): shore distance is baked into vertex colour R
// (0 = at the shoreline, 1 = open sea). Waves move vertices; two scrolling normal maps give sparkle.
// Single Pass Instanced safe.
Shader "Gamebreak/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow Colour", Color) = (0.25, 0.9, 0.85, 0.55)
        _DeepColor ("Deep Colour", Color) = (0.02, 0.35, 0.62, 0.95)
        _HorizonColor ("Horizon / Sky Reflection", Color) = (0.7, 0.9, 1, 1)
        _FoamColor ("Foam Colour", Color) = (1, 1, 1, 1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Tiling (1/m)", Float) = 0.18
        _NormalStrength ("Normal Strength", Range(0, 1)) = 0.45
        _FoamTex ("Foam Noise", 2D) = "gray" {}
        _FoamWidth ("Foam Width (shore factor)", Range(0, 0.3)) = 0.07
        _WaveHeight ("Wave Height (m)", Float) = 0.04
        _WaveSpeed ("Wave Speed", Float) = 0.6
        _ScrollSpeed ("Normal Scroll Speed", Float) = 0.03
        _Gloss ("Gloss", Range(8, 512)) = 180
        _SpecStrength ("Specular", Range(0, 3)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-10" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "ForwardWater"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamTex);   SAMPLER(sampler_FoamTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _HorizonColor;
                half4 _FoamColor;
                float _NormalScale;
                half  _NormalStrength;
                half  _FoamWidth;
                float _WaveHeight;
                float _WaveSpeed;
                float _ScrollSpeed;
                half  _Gloss;
                half  _SpecStrength;
                float4 _NormalMap_ST;
                float4 _FoamTex_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half   shore      : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;
                // Waves grow away from the shore so the waterline stays put.
                float amp = _WaveHeight * saturate(input.color.r * 4.0);
                p.y += (sin(p.x * 0.35 + t) * 0.6 + sin(p.z * 0.27 + t * 1.3) * 0.4) * amp;
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.shore = input.color.r;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half3 SampleWaterNormal(float2 uv, half strength)
            {
                half3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv), strength);
                return n;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float t = _Time.y * _ScrollSpeed;
                float2 uv = input.positionWS.xz * _NormalScale;
                half3 n1 = SampleWaterNormal(uv + float2(t, t * 0.6), _NormalStrength);
                half3 n2 = SampleWaterNormal(uv * 1.7 + float2(-t * 0.7, t), _NormalStrength);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                half3 n = normalize(half3(nTS.x, nTS.z, nTS.y)); // tangent space (xy) -> world (xz) for a flat surface

                half depth = saturate(input.shore);
                half4 water = lerp(_ShallowColor, _DeepColor, smoothstep(0.0h, 0.6h, depth));

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(n, viewWS)), 4.0h);
                half3 col = lerp(water.rgb, _HorizonColor.rgb, fresnel * 0.6h);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 h = SafeNormalize(light.direction + viewWS);
                half spec = pow(saturate(dot(n, h)), _Gloss) * _SpecStrength * light.shadowAttenuation;
                col = col * (SampleSH(half3(0, 1, 0)) * 0.6h + light.color * 0.55h * (0.5h + 0.5h * light.shadowAttenuation));
                col += light.color * spec;

                // Shore foam: a band that breathes in and out, broken up by noise.
                half foamNoise = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, input.positionWS.xz * 0.35 + float2(t * 2.0, 0)).r;
                half band = _FoamWidth * (0.75h + 0.25h * sin(_Time.y * 1.4h + input.positionWS.x * 0.3h));
                half foam = saturate((band - depth) / max(band, 1e-3h)) ;
                foam = step(0.45h, foam * (0.6h + foamNoise * 0.8h));
                col = lerp(col, _FoamColor.rgb, foam * _FoamColor.a);
                half alpha = saturate(water.a + fresnel * 0.3h + foam);

                col = MixFog(col, input.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
