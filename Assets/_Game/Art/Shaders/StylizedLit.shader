// Gamebreak stylized lit shader for the world art kit.
// - Albedo = base map (palette or texture, UV0) x base colour x vertex colour RGB, with optional world-space grain.
// - Optional tangent-space normal map (_NORMALMAP), mask map (_MASKMAP: G occlusion, A smoothness),
//   alpha-tested cut-outs (_ALPHATEST_ON) and leaf translucency.
// - Main light with shadows, sky ambient (SH), specular, rim light, optional emission.
// - Vertex colour alpha = wind sway weight. Optional two-sided lighting (Cull Off) for leaves.
// - Written for URP 17 and Single Pass Instanced stereo (instancing + stereo macros in every pass).
Shader "Gamebreak/StylizedLit"
{
    Properties
    {
        _BaseMap ("Base Map (palette or texture)", 2D) = "white" {}
        _BaseColor ("Base Colour", Color) = (1, 1, 1, 1)
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 2)) = 1
        [Toggle(_MASKMAP)] _UseMaskMap ("Use Mask Map", Float) = 0
        _MaskMap ("Mask (G occlusion, A smoothness)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Cut-out", Float) = 0
        _Cutoff ("Cut-out Threshold", Range(0, 1)) = 0.5
        _DetailTex ("Detail (grayscale, tiling)", 2D) = "gray" {}
        _DetailScale ("Detail World Scale (1/m)", Float) = 1
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.25
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _Specular ("Specular", Range(0, 1)) = 0.08
        _RimColor ("Rim Colour (A = strength)", Color) = (1, 0.95, 0.8, 0.15)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _AmbientBoost ("Ambient Boost", Range(0, 2)) = 1
        _Translucency ("Leaf Translucency", Range(0, 2)) = 0
        _TranslucencyColor ("Translucency Tint", Color) = (0.8, 1, 0.4, 1)
        _WindStrength ("Wind Strength (m)", Float) = 0
        _WindSpeed ("Wind Speed", Float) = 1.2
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _MASKMAP
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "StylizedCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3  normalWS   : TEXCOORD2;
                half4  color      : TEXCOORD3;
                half   fogFactor  : TEXCOORD4;
            #if defined(_NORMALMAP)
                half4  tangentWS  : TEXCOORD5;
            #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS = ApplyWind(positionWS, input.color.a);
                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                VertexNormalInputs ni = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.normalWS = ni.normalWS;
            #if defined(_NORMALMAP)
                real sign = input.tangentOS.w * GetOddNegativeScale();
                o.tangentWS = half4(ni.tangentWS, sign);
            #endif
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
            #if defined(_ALPHATEST_ON)
                clip(baseSample.a - _Cutoff);
            #endif
                half faceSign = IS_FRONT_VFACE(facing, 1.0h, -1.0h);
                half3 n = normalize(input.normalWS) * faceSign;
            #if defined(_NORMALMAP)
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 t = normalize(input.tangentWS.xyz);
                half3 b = cross(n, t) * input.tangentWS.w;
                n = normalize(mul(nTS, half3x3(t, b, n)));
            #endif

                half occlusion = 1.0h;
                half smoothness = _Smoothness;
            #if defined(_MASKMAP)
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                occlusion = lerp(1.0h, mask.g, _OcclusionStrength);
                smoothness *= mask.a;
            #endif

                half3 albedo = baseSample.rgb * _BaseColor.rgb * input.color.rgb;
                if (_DetailStrength > 0.001h)
                {
                    half grain = TriplanarDetail(input.positionWS, n);
                    albedo *= lerp(1.0h, grain * 2.0h, _DetailStrength);
                }

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light light = GetMainLight(shadowCoord);
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half ndl = dot(n, light.direction);
                // Slightly softened terminator for a painted look.
                half diffuse = saturate(ndl * 0.85h + 0.15h) * atten;

                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 h = SafeNormalize(light.direction + viewWS);
                half specPow = exp2(10.0h * smoothness + 1.0h);
                half spec = pow(saturate(dot(n, h)), specPow) * _Specular * atten * smoothness * 2.0h;
                half rim = pow(1.0h - saturate(dot(n, viewWS)), _RimPower) * _RimColor.a * (0.35h + 0.65h * diffuse);

                // Sunlight glowing through leaves when looking toward the sun.
                half back = pow(saturate(dot(viewWS, -light.direction)), 4.0h) * _Translucency * light.shadowAttenuation;
                half3 translucent = back * _TranslucencyColor.rgb * light.color;

                half3 ambient = SampleSH(n) * _AmbientBoost * occlusion;
                half3 color = albedo * (ambient + light.color * diffuse * lerp(0.6h, 1.0h, occlusion) + translucent)
                            + light.color * spec
                            + _RimColor.rgb * rim
                            + _EmissionColor.rgb;
                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "StylizedCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color.a);
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
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
            #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "StylizedCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformWorldToHClip(ApplyWind(TransformObjectToWorld(input.positionOS.xyz), input.color.a));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
