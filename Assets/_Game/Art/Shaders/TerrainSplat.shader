// Gamebreak terrain: four textured layers (sand, lawn, sandstone rock, gravel path) blended by vertex
// colour weights (R sand, G lawn, B rock, A path) with height-aware transitions. Flat layers are
// projected from above; rock is triplanar so slopes and cliffs never stretch. The seabed (below sea
// level) gets a depth tint. URP 17, Single Pass Instanced safe.
Shader "Gamebreak/TerrainSplat"
{
    Properties
    {
        _SandAlb ("Sand Albedo", 2D) = "white" {}
        [Normal] _SandNrm ("Sand Normal", 2D) = "bump" {}
        _LawnAlb ("Lawn Albedo", 2D) = "white" {}
        [Normal] _LawnNrm ("Lawn Normal", 2D) = "bump" {}
        _RockAlb ("Rock Albedo", 2D) = "white" {}
        [Normal] _RockNrm ("Rock Normal", 2D) = "bump" {}
        _PathAlb ("Path Albedo", 2D) = "white" {}
        [Normal] _PathNrm ("Path Normal", 2D) = "bump" {}
        _Tiling ("Tile Size m (sand, lawn, rock, path)", Vector) = (3, 2.5, 4, 2.5)
        _Smooth ("Smoothness (sand, lawn, rock, path)", Vector) = (0.15, 0.2, 0.15, 0.1)
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1
        _BlendSharpness ("Blend Sharpness", Range(1, 16)) = 6
        _LawnTint ("Lawn Tint", Color) = (1, 1, 1, 1)
        _SeaLevel ("Sea Level", Float) = 0
        _SeabedTint ("Seabed Tint", Color) = (0.55, 0.85, 0.82, 1)
        _MacroScale ("Macro Variation Scale (1/m)", Float) = 0.035
        _MacroTex ("Macro Variation (grayscale)", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_SandAlb); TEXTURE2D(_SandNrm);
        TEXTURE2D(_LawnAlb); TEXTURE2D(_LawnNrm);
        TEXTURE2D(_RockAlb); TEXTURE2D(_RockNrm);
        TEXTURE2D(_PathAlb); TEXTURE2D(_PathNrm);
        TEXTURE2D(_MacroTex);
        SAMPLER(sampler_linear_repeat);

        CBUFFER_START(UnityPerMaterial)
            float4 _Tiling;
            half4 _Smooth;
            half _NormalStrength;
            half _BlendSharpness;
            half4 _LawnTint;
            float _SeaLevel;
            half4 _SeabedTint;
            float _MacroScale;
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 weights : TEXCOORD2;
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
                o.weights = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            // Top-down layer: albedo (rgb) + pseudo-height (luminance) and a world-space normal.
            void TopLayer(TEXTURE2D_PARAM(alb, s), TEXTURE2D_PARAM(nrm, s2), float2 uv, half3 n, out half4 a, out half3 nw)
            {
                a = SAMPLE_TEXTURE2D(alb, s, uv);
                a.a = dot(a.rgb, half3(0.3h, 0.59h, 0.11h));
                half3 t = UnpackNormalScale(SAMPLE_TEXTURE2D(nrm, s2, uv), _NormalStrength);
                nw = normalize(half3(n.x + t.x, n.y, n.z + t.y)); // UDN blend onto the surface
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 n = normalize(input.normalWS);
                float3 p = input.positionWS;
                half4 w = max(input.weights, 0.0h);

                half4 sandA, lawnA, pathA; half3 sandN, lawnN, pathN;
                TopLayer(TEXTURE2D_ARGS(_SandAlb, sampler_linear_repeat), TEXTURE2D_ARGS(_SandNrm, sampler_linear_repeat), p.xz / _Tiling.x, n, sandA, sandN);
                TopLayer(TEXTURE2D_ARGS(_LawnAlb, sampler_linear_repeat), TEXTURE2D_ARGS(_LawnNrm, sampler_linear_repeat), p.xz / _Tiling.y, n, lawnA, lawnN);
                TopLayer(TEXTURE2D_ARGS(_PathAlb, sampler_linear_repeat), TEXTURE2D_ARGS(_PathNrm, sampler_linear_repeat), p.xz / _Tiling.w, n, pathA, pathN);
                lawnA.rgb *= _LawnTint.rgb;

                // Triplanar rock.
                half3 bw = pow(abs(n), 4.0h); bw /= (bw.x + bw.y + bw.z);
                float3 rp = p / _Tiling.z;
                half4 rx = SAMPLE_TEXTURE2D(_RockAlb, sampler_linear_repeat, rp.zy);
                half4 ry = SAMPLE_TEXTURE2D(_RockAlb, sampler_linear_repeat, rp.xz);
                half4 rz = SAMPLE_TEXTURE2D(_RockAlb, sampler_linear_repeat, rp.xy);
                half4 rockA = rx * bw.x + ry * bw.y + rz * bw.z;
                rockA.a = dot(rockA.rgb, half3(0.3h, 0.59h, 0.11h));
                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNrm, sampler_linear_repeat, rp.zy), _NormalStrength);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNrm, sampler_linear_repeat, rp.xz), _NormalStrength);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_RockNrm, sampler_linear_repeat, rp.xy), _NormalStrength);
                // Whiteout triplanar normal blend.
                half3 nx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                half3 ny = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                half3 nz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                half3 rockN = normalize(nx.zyx * bw.x + ny.xzy * bw.y + nz.xyz * bw.z);

                // Height-aware blend: brighter (higher) texels win at transitions.
                half4 hb = w * (half4(sandA.a, lawnA.a, rockA.a, pathA.a) * 0.6h + 0.4h);
                hb = pow(hb + 1e-4h, _BlendSharpness);
                hb /= dot(hb, 1.0h);

                half3 albedo = sandA.rgb * hb.x + lawnA.rgb * hb.y + rockA.rgb * hb.z + pathA.rgb * hb.w;
                half3 nrm = normalize(sandN * hb.x + lawnN * hb.y + rockN * hb.z + pathN * hb.w);
                half smooth = dot(hb, _Smooth);

                // Large-scale variation breaks up tiling.
                half macro = SAMPLE_TEXTURE2D(_MacroTex, sampler_linear_repeat, p.xz * _MacroScale).r;
                albedo *= lerp(0.82h, 1.12h, macro);

                // Seabed: darken and tint with depth below sea level.
                half depth = saturate((_SeaLevel - p.y) * 0.45h);
                albedo *= lerp(half3(1, 1, 1), _SeabedTint.rgb, depth);

                Light light = GetMainLight(TransformWorldToShadowCoord(p));
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half diffuse = saturate(dot(nrm, light.direction) * 0.9h + 0.1h) * atten;
                half3 viewWS = GetWorldSpaceNormalizeViewDir(p);
                half3 h = SafeNormalize(light.direction + viewWS);
                half spec = pow(saturate(dot(nrm, h)), exp2(10.0h * smooth + 1.0h)) * smooth * 0.25h * atten;
                half3 color = albedo * (SampleSH(nrm) + light.color * diffuse) + light.color * spec;
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
