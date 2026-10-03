// Gamebreak hero rock: triplanar sandstone (albedo + normal) with lawn growing on upward-facing
// surfaces, ambient occlusion baked into vertex colour R (by the Blender pipeline), and an overall tint.
// No UVs needed, so sculpted/decimated meshes texture cleanly. URP 17, Single Pass Instanced safe.
Shader "Gamebreak/RockTriplanar"
{
    Properties
    {
        _RockAlb ("Rock Albedo", 2D) = "white" {}
        [Normal] _RockNrm ("Rock Normal", 2D) = "bump" {}
        _TopAlb ("Top (moss/grass) Albedo", 2D) = "white" {}
        [Normal] _TopNrm ("Top Normal", 2D) = "bump" {}
        _RockTile ("Rock Tile Size (m)", Float) = 3
        _TopTile ("Top Tile Size (m)", Float) = 2
        _TopCoverage ("Top Coverage (normal.y threshold)", Range(0, 1)) = 0.62
        _TopSoftness ("Top Edge Softness", Range(0.01, 0.5)) = 0.12
        _Tint ("Rock Tint", Color) = (1, 1, 1, 1)
        _TopTint ("Top Tint", Color) = (1, 1, 1, 1)
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.2
        _AOStrength ("Baked AO Strength", Range(0, 1)) = 1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
        _NoiseTex ("Edge Noise (grayscale)", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_RockAlb); TEXTURE2D(_RockNrm); TEXTURE2D(_TopAlb); TEXTURE2D(_TopNrm); TEXTURE2D(_NoiseTex);
        SAMPLER(sampler_linear_repeat);
        CBUFFER_START(UnityPerMaterial)
            float _RockTile, _TopTile;
            half _TopCoverage, _TopSoftness, _NormalStrength, _AOStrength, _Smoothness;
            half4 _Tint, _TopTint;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            void Triplanar(TEXTURE2D_PARAM(alb, s), TEXTURE2D_PARAM(nrm, s2), float3 p, half3 n, half3 bw, out half3 a, out half3 nw)
            {
                half3 ax = SAMPLE_TEXTURE2D(alb, s, p.zy).rgb, ay = SAMPLE_TEXTURE2D(alb, s, p.xz).rgb, az = SAMPLE_TEXTURE2D(alb, s, p.xy).rgb;
                a = ax * bw.x + ay * bw.y + az * bw.z;
                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(nrm, s2, p.zy), _NormalStrength);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(nrm, s2, p.xz), _NormalStrength);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(nrm, s2, p.xy), _NormalStrength);
                half3 nx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                half3 ny = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                half3 nz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                nw = normalize(nx.zyx * bw.x + ny.xzy * bw.y + nz.xyz * bw.z);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 n = normalize(input.normalWS);
                half3 bw = pow(abs(n), 4.0h); bw /= (bw.x + bw.y + bw.z);
                float3 p = input.positionWS;

                half3 rockA, rockN, topA, topN;
                Triplanar(TEXTURE2D_ARGS(_RockAlb, sampler_linear_repeat), TEXTURE2D_ARGS(_RockNrm, sampler_linear_repeat), p / _RockTile, n, bw, rockA, rockN);
                Triplanar(TEXTURE2D_ARGS(_TopAlb, sampler_linear_repeat), TEXTURE2D_ARGS(_TopNrm, sampler_linear_repeat), p / _TopTile, n, bw, topA, topN);
                rockA *= _Tint.rgb;
                topA *= _TopTint.rgb;

                // Moss/grass on top: by surface slope, with a noisy ragged edge.
                half edgeNoise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_linear_repeat, p.xz * 0.35).r - 0.5h;
                half top = smoothstep(_TopCoverage - _TopSoftness, _TopCoverage + _TopSoftness, n.y + edgeNoise * 0.35h);
                half3 albedo = lerp(rockA, topA, top);
                half3 nrm = normalize(lerp(rockN, topN, top));

                half ao = lerp(1.0h, input.color.r, _AOStrength);
                Light light = GetMainLight(TransformWorldToShadowCoord(p));
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half diffuse = saturate(dot(nrm, light.direction) * 0.9h + 0.1h) * atten;
                half3 viewWS = GetWorldSpaceNormalizeViewDir(p);
                half3 h = SafeNormalize(light.direction + viewWS);
                half spec = pow(saturate(dot(nrm, h)), exp2(10.0h * _Smoothness + 1.0h)) * _Smoothness * 0.3h * atten;
                half3 color = albedo * (SampleSH(nrm) * ao + light.color * diffuse * lerp(0.55h, 1.0h, ao)) + light.color * spec;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 ShadowVert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }
            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
