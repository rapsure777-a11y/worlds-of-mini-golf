#ifndef GAMEBREAK_STYLIZED_COMMON_INCLUDED
#define GAMEBREAK_STYLIZED_COMMON_INCLUDED

// Shared by every pass of Gamebreak/StylizedLit. Keep the CBUFFER identical across passes (SRP Batcher).
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);    SAMPLER(sampler_BaseMap);
TEXTURE2D(_DetailTex);  SAMPLER(sampler_DetailTex);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    float  _DetailScale;
    half   _DetailStrength;
    half   _Smoothness;
    half   _Specular;
    half4  _RimColor;
    half   _RimPower;
    half   _AmbientBoost;
    float  _WindStrength;
    float  _WindSpeed;
    half4  _EmissionColor;
    float  _Cull;
CBUFFER_END

// Gentle two-frequency sway. weight comes from vertex colour alpha (0 = rigid, 1 = full sway).
float3 ApplyWind(float3 positionWS, float weight)
{
    float t = _Time.y * _WindSpeed;
    float phase = dot(positionWS.xz, float2(0.37, 0.23));
    float s = sin(t + phase) * 0.65 + sin(t * 2.3 + phase * 1.9) * 0.35;
    float lift = sin(t * 1.7 + phase * 1.3);
    positionWS.xz += float2(s, s * 0.55) * (_WindStrength * weight);
    positionWS.y += lift * (_WindStrength * weight) * 0.25;
    return positionWS;
}

// World-space triplanar sample of the grayscale detail texture, centred on 0.5.
half TriplanarDetail(float3 positionWS, half3 normalWS)
{
    half3 w = abs(normalWS);
    w = w / max(w.x + w.y + w.z, 1e-4h);
    float3 p = positionWS * _DetailScale;
    half dx = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, p.zy).r;
    half dy = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, p.xz).r;
    half dz = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, p.xy).r;
    return dx * w.x + dy * w.y + dz * w.z;
}

#endif
