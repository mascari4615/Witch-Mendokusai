#ifndef WM_WIND_INCLUDED
#define WM_WIND_INCLUDED

// 잎과 풀의 바람. xy 는 방향과 세기 (m), z 는 빠르기, w 는 땅 높이. 안 켠 스테이지는 0 이라 그대로
float4 _WM_Wind;

// weight 0 이면 안 움직임 (뿌리), 1 이면 끝
float3 WMWind(float3 positionWS, float weight)
{
    float along = dot(positionWS.xz, normalize(_WM_Wind.xy + 1e-5));
    float time = _Time.y * _WM_Wind.z;
    // 지나가는 바람결 하나와 잔떨림 하나
    float gust = sin(along * 0.35 - time) * 0.5 + 0.5;
    float flutter = sin(dot(positionWS, float3(3.1, 2.3, 2.7)) + time * 2.6);
    float lift = saturate(positionWS.y - _WM_Wind.w) * weight;
    return float3(_WM_Wind.x, 0, _WM_Wind.y) * ((gust * 1.3 - 0.3) + flutter * 0.28) * lift;
}

#endif
