// Gamebreak tropical ocean / lagoon (v2).
// Uses the camera depth and opaque textures (enabled in the URP asset) for:
//  - depth-based colour absorption (clear turquoise shallows -> deep blue),
//  - refraction of the seabed through the surface,
//  - intersection foam wherever the water meets sand, rocks or the pier.
// Vertex colour R still carries a baked shore distance for broad surf bands and wave damping.
// Single Pass Instanced safe (XR-aware screen UVs and texture-array sampling).
Shader "Gamebreak/StylizedWater"
{
    Properties
    {
        _ShallowColor ("Shallow Colour", Color) = (0.25, 0.95, 0.85, 1)
        _DeepColor ("Deep Colour", Color) = (0.02, 0.30, 0.58, 1)
        _HorizonColor ("Horizon / Sky Reflection", Color) = (0.72, 0.9, 1, 1)
        _FoamColor ("Foam Colour", Color) = (1, 1, 1, 1)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalScale ("Normal Tiling (1/m)", Float) = 0.18
        _NormalStrength ("Normal Strength", Range(0, 1)) = 0.45
        _FoamTex ("Foam Noise", 2D) = "gray" {}
        _FoamWidth ("Shore Foam Width (shore factor)", Range(0, 0.3)) = 0.06
        _IntersectFoam ("Intersection Foam Depth (m)", Range(0, 1)) = 0.35
        _Absorption ("Depth Absorption (1/m)", Range(0.05, 3)) = 0.55
        _Clarity ("Shallow Clarity", Range(0, 1)) = 0.75
        _Refraction ("Refraction Strength", Range(0, 0.1)) = 0.03
        _WaveHeight ("Wave Height (m)", Float) = 0.05
        _WaveSpeed ("Wave Speed", Float) = 0.6
        _ScrollSpeed ("Normal Scroll Speed", Float) = 0.03
        _Gloss ("Gloss", Range(8, 1024)) = 400
        _SpecStrength ("Specular", Range(0, 3)) = 0.8
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_FoamTex);   SAMPLER(sampler_FoamTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _DeepColor, _HorizonColor, _FoamColor;
                float _NormalScale;
                half  _NormalStrength, _FoamWidth, _IntersectFoam, _Absorption, _Clarity, _Refraction;
                float _WaveHeight, _WaveSpeed, _ScrollSpeed;
                half  _Gloss, _SpecStrength;
                float4 _NormalMap_ST, _FoamTex_ST;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half shore : TEXCOORD1;
                half fogFactor : TEXCOORD2;
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
                float amp = _WaveHeight * saturate(input.color.r * 4.0);
                p.y += (sin(p.x * 0.35 + t) * 0.6 + sin(p.z * 0.27 + t * 1.3) * 0.4) * amp;
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.shore = input.color.r;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float t = _Time.y * _ScrollSpeed;
                float2 uv = input.positionWS.xz * _NormalScale;
                half3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv + float2(t, t * 0.6)), _NormalStrength);
                half3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, uv * 1.7 + float2(-t * 0.7, t)), _NormalStrength);
                half3 nTS = normalize(half3(n1.xy + n2.xy, n1.z * n2.z));
                half3 n = normalize(half3(nTS.x, nTS.z, nTS.y));

                // Depth of water behind this pixel, from the camera depth texture.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float surfaceEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                half waterDepth = max(0.0h, sceneEye - surfaceEye);

                // Refraction: offset the lookup by the ripple normal, but never pick up things in front.
                float2 refrUV = screenUV + n.xz * _Refraction * saturate(waterDepth);
                float refrEye = LinearEyeDepth(SampleSceneDepth(refrUV), _ZBufferParams);
                if (refrEye < surfaceEye) refrUV = screenUV;
                half3 below = SampleSceneColor(refrUV);

                half absorb = 1.0h - exp(-waterDepth * _Absorption);
                half3 waterTint = lerp(_ShallowColor.rgb, _DeepColor.rgb, absorb);
                half3 col = lerp(below * _ShallowColor.rgb, waterTint, saturate(absorb + (1.0h - _Clarity)));

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1.0h - saturate(dot(n, viewWS)), 5.0h);
                col = lerp(col, _HorizonColor.rgb, fresnel * 0.7h);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 h = SafeNormalize(light.direction + viewWS);
                half spec = pow(saturate(dot(n, h)), _Gloss) * _SpecStrength * light.shadowAttenuation;
                col += light.color * spec;

                // Foam: around anything the water touches, plus breathing surf bands at the shoreline.
                half foamNoise = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, input.positionWS.xz * 0.35 + float2(t * 2.0, 0)).r;
                half intersect = saturate(1.0h - waterDepth / max(_IntersectFoam, 1e-3h));
                half band = _FoamWidth * (0.75h + 0.25h * sin(_Time.y * 1.4h + input.positionWS.x * 0.3h));
                half surf = saturate((band - input.shore) / max(band, 1e-3h));
                half foam = step(0.5h, max(intersect, surf) * (0.55h + foamNoise * 0.9h));
                col = lerp(col, _FoamColor.rgb, foam * _FoamColor.a);

                // Fully opaque result: the refraction already carries what lies beneath.
                col = MixFog(col, input.fogFactor);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
