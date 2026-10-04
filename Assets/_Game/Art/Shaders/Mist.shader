// Gamebreak mist / spray particle: soft textured billboard, alpha blended, unlit, fog-aware.
// Tint comes from the particle vertex colour. When _GB_WATER_DEPTH is on (depth texture available) it
// fades where it meets geometry (soft particles). Single Pass Instanced safe.
Shader "Gamebreak/Mist"
{
    Properties
    {
        _BaseMap ("Soft Dot (alpha)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 0.35)
        _SoftDistance ("Soft Particle Distance (m)", Range(0.05, 3)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "ForwardMist"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _GB_WATER_DEPTH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _BaseMap_ST;
                half _SoftDistance;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 c = tex * _BaseColor * input.color;
#if defined(_GB_WATER_DEPTH)
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float myEye = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                c.a *= saturate((sceneEye - myEye) / _SoftDistance);
#endif
                c.rgb = MixFog(c.rgb, input.fogFactor);
                return c;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
