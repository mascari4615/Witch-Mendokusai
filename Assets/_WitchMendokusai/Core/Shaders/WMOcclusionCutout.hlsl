// 캐릭터 가림 컷아웃. 캐릭터 화면 위치 둘레 원 안, 캐릭터보다 카메라에 가깝고 발보다 높은 면만
// 값은 전역, CameraOcclusionCutout 이 카메라마다 설정. 재질의 _WM_OCCLUDABLE 키워드가 꺼져 있으면 0
#ifndef WM_OCCLUSION_CUTOUT_INCLUDED
#define WM_OCCLUSION_CUTOUT_INCLUDED

float4 _WM_CutoutCenter;     // xy: 화면 비율 좌표 (왼쪽 아래 0)
float _WM_CutoutRadius;      // 화면 높이 비율, 0 이면 꺼짐
float _WM_CutoutSoftness;    // 화면 높이 비율
float _WM_CutoutDepth;       // 카메라 앞방향 거리, 이보다 가까운 면만
float _WM_CutoutFloorY;      // 월드 높이, 이보다 높은 면만
float _WM_CutoutMaxCoverage; // 원 가운데 비우는 비율
float _WM_CutoutNearDistance; // 카메라 앞방향 거리, 이보다 가까운 면은 원 밖도 비움 (카메라가 잎 속에 들어갈 때)
float _WM_CutoutNearSoftness; // 위 거리에서 점이 줄어드는 폭

// 4x4 bayer 문턱값 (0~1)
float WMBayer4(float2 pixel)
{
    static const float BAYER[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    uint2 cell = uint2(pixel) % 4u;
    return (BAYER[cell.y * 4u + cell.x] + 0.5) / 16.0;
}

// 카메라 바로 앞 비움 비율만. 원 조건 없음
float WMCutoutNearCoverage(float3 positionWS)
{
    float eyeDepth = -TransformWorldToView(positionWS).z;
    return saturate((_WM_CutoutNearDistance - eyeDepth) / max(_WM_CutoutNearSoftness, 1e-4));
}

// screenPosition: ComputeScreenPos 결과 (w 로 나누기 전). 0 그대로, 1 다 비움
float WMCutoutCoverage(float4 screenPosition, float3 positionWS)
{
#if defined(_WM_OCCLUDABLE)
    float2 uv = screenPosition.xy / screenPosition.w;
    float2 delta = (uv - _WM_CutoutCenter.xy) * float2(_ScreenParams.x / _ScreenParams.y, 1.0);
    float edge = saturate((_WM_CutoutRadius - length(delta)) / max(_WM_CutoutSoftness, 1e-4));
    float eyeDepth = -TransformWorldToView(positionWS).z;
    float nearer = step(eyeDepth, _WM_CutoutDepth);
    float above = step(_WM_CutoutFloorY, positionWS.y);
    float circle = edge * nearer * above * _WM_CutoutMaxCoverage;
    return max(circle, WMCutoutNearCoverage(positionWS));
#else
    return 0.0;
#endif
}

// 불투명 면: 비율만큼 점무늬로 버림. 비율 0 이면 그대로
void WMCutoutClip(float coverage, float2 pixel)
{
    clip(WMBayer4(pixel) - coverage);
}

#endif
