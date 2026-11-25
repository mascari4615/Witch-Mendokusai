// 마을 하늘. 하늘 상자와 물 반사가 같은 식을 씀
// 색은 시간대 전역값 (_WMSky*), 해 방향은 마을이 눕힌 빛 (_VillageSunDirection)
#ifndef VILLAGE_SKY_INCLUDED
#define VILLAGE_SKY_INCLUDED

float4 _WMSkyZenith;
float4 _WMSkyHorizon;
float4 _WMSkySun;
float4 _VillageSunDirection;
float4 _VillageSunColor;

float SkyHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float SkyNoise(float2 p)
{
    float2 cell = floor(p);
    float2 inside = frac(p);
    inside = inside * inside * (3.0 - 2.0 * inside);
    float low = lerp(SkyHash(cell), SkyHash(cell + float2(1, 0)), inside.x);
    float high = lerp(SkyHash(cell + float2(0, 1)), SkyHash(cell + float2(1, 1)), inside.x);
    return lerp(low, high, inside.y);
}

float SkyFractal(float2 p, int octaves)
{
    float sum = 0.0;
    float weight = 0.5;
    for (int i = 0; i < octaves; i++)
    {
        sum += SkyNoise(p) * weight;
        p = float2(p.x * 1.6 + p.y * 1.2, -p.x * 1.2 + p.y * 1.6);
        weight *= 0.5;
    }
    return sum;
}

float3 SkyZenithColor() { return _WMSkyZenith.rgb * float3(0.50, 0.74, 1.0); }

float3 SkyHorizonColor() { return lerp(_WMSkyHorizon.rgb, _WMSkyZenith.rgb, 0.42) * float3(0.94, 1.0, 1.04); }

float3 SkyBase(float3 dir)
{
    float up = saturate(dir.y);
    float3 sky = lerp(SkyHorizonColor(), SkyZenithColor(), pow(up, 0.55));
    float facing = saturate(dot(dir, _VillageSunDirection.xyz));
    sky += _VillageSunColor.rgb * (pow(facing, 6.0) * 0.10 + pow(facing, 60.0) * 0.28);
    return sky;
}

// 뭉게구름. 밑은 그늘, 해 쪽 가장자리는 밝게
float4 SkyCloud(float3 dir, int octaves)
{
    if (dir.y < 0.015)
        return 0.0;
    float2 plane_ = dir.xz / (dir.y + 0.16);
    float2 uv = plane_ * 0.95 + float2(_Time.y * 0.004, _Time.y * 0.0016);
    float shape = SkyFractal(uv, octaves);
    float big = SkyNoise(uv * 0.42 + 7.3);
    float density = smoothstep(0.50, 0.66, shape * 0.82 + big * 0.28);
    float2 toward = _VillageSunDirection.xz * 0.16;
    float ahead = smoothstep(0.46, 0.74, SkyFractal(uv + toward, octaves) * 0.82 + big * 0.28);
    float lit = saturate(0.62 + (density - ahead) * 0.9 + (0.66 - shape) * 0.5);
    float3 bright = saturate(_VillageSunColor.rgb * 0.95 + 0.12);
    float3 shade = lerp(SkyHorizonColor(), SkyZenithColor(), 0.35) * 0.92;
    float facing = saturate(dot(dir, _VillageSunDirection.xyz));
    float3 color = lerp(shade, bright, lit) + _VillageSunColor.rgb * pow(facing, 10.0) * 0.18;
    float fade = smoothstep(0.015, 0.16, dir.y);
    return float4(color, density * fade * 0.95);
}

float3 VillageSky(float3 dir, int octaves)
{
    float3 sky = SkyBase(dir);
    float facing = dot(dir, _VillageSunDirection.xyz);
    sky = lerp(sky, _VillageSunColor.rgb * 1.6, smoothstep(0.9975, 0.9988, facing));
    float4 cloud = SkyCloud(dir, octaves);
    return lerp(sky, cloud.rgb, cloud.a);
}
#endif
