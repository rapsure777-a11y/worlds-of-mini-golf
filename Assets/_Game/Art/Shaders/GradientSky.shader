// Stylised tropical sky: three-colour vertical gradient with a soft sun disc and glow.
// Replaces Skybox/Procedural, whose low-atmosphere horizon turned green. Single Pass Instanced safe.
Shader "Gamebreak/GradientSky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.22, 0.52, 0.95, 1)
        _HorizonColor ("Horizon", Color) = (0.72, 0.9, 1, 1)
        _BottomColor ("Below Horizon", Color) = (0.55, 0.78, 0.9, 1)
        _Exponent ("Gradient Exponent", Range(0.1, 4)) = 0.55
        _SunColor ("Sun", Color) = (1, 0.95, 0.8, 1)
        _SunSize ("Sun Size", Range(0.001, 0.1)) = 0.02
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _HorizonColor, _BottomColor, _SunColor;
                half _Exponent, _SunSize, _SunGlow;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 d = normalize(input.dir);
                half up = saturate(d.y);
                half3 col = lerp(_HorizonColor.rgb, _TopColor.rgb, pow(up, _Exponent));
                col = lerp(col, _BottomColor.rgb, saturate(-d.y * 6.0));
                half sunDot = saturate(dot(d, _MainLightPosition.xyz));
                half disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.6, sunDot);
                half glow = pow(sunDot, 64.0) * _SunGlow + pow(sunDot, 8.0) * _SunGlow * 0.25;
                col += _SunColor.rgb * (disc * 2.0 + glow);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
