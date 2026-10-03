// Gamebreak waterfall sheet: streaky falling water scrolling along UV.y (0 = top, 1 = bottom), white
// foam breaking up near the lip and the plunge, soft side edges, gentle sway. Transparent, two-sided.
// Single Pass Instanced safe.
Shader "Gamebreak/Waterfall"
{
    Properties
    {
        _WaterColor ("Water Colour", Color) = (0.55, 0.88, 0.95, 0.75)
        _FoamColor ("Foam Colour", Color) = (1, 1, 1, 1)
        _NoiseTex ("Streak Noise", 2D) = "gray" {}
        _Speed ("Fall Speed", Float) = 1.6
        _StreakScale ("Streak Scale (x, y)", Vector) = (6, 1.2, 0, 0)
        _FoamTop ("Foam at Lip", Range(0, 0.5)) = 0.12
        _FoamBottom ("Foam at Plunge", Range(0, 0.5)) = 0.25
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.15
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "ForwardWaterfall"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _WaterColor, _FoamColor;
                float _Speed; float4 _StreakScale;
                half _FoamTop, _FoamBottom, _EdgeSoftness;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 p = TransformObjectToWorld(input.positionOS.xyz);
                p.xz += sin(_Time.y * 2.0 + input.uv.y * 9.0 + p.y) * 0.03 * input.uv.y;
                o.positionWS = p;
                o.positionCS = TransformWorldToHClip(p);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _Time.y * _Speed;
                float2 uvA = input.uv * _StreakScale.xy + float2(0, -t);
                float2 uvB = input.uv * _StreakScale.xy * float2(1.7, 0.6) + float2(0.31, -t * 1.35);
                half s = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvA).r * 0.6h + SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, uvB).r * 0.4h;
                half streak = smoothstep(0.45h, 0.75h, s);
                half foamZone = saturate(1.0h - input.uv.y / max(_FoamTop, 1e-3h)) + saturate((input.uv.y - (1.0h - _FoamBottom)) / max(_FoamBottom, 1e-3h));
                half foam = saturate(streak + foamZone * (0.6h + s));
                half edge = smoothstep(0.0h, _EdgeSoftness, input.uv.x) * smoothstep(1.0h, 1.0h - _EdgeSoftness, input.uv.x);
                half3 lightCol = GetMainLight().color;
                half3 ambient = SampleSH(half3(0, 1, 0));
                half3 col = lerp(_WaterColor.rgb, _FoamColor.rgb, foam) * (ambient * 0.6h + lightCol * 0.7h);
                half alpha = saturate(lerp(_WaterColor.a, 1.0h, foam)) * edge;
                col = MixFog(col, input.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
