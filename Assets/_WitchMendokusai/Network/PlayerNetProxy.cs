using UnityEngine;
using WitchMendokusai.DomainSDK.Network;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-191 step-1 — 네트워크 플레이어 프록시. owner 피어가 *자기 로컬 플레이어 위치*(ILocalPlayerProbe)
    /// 를 따라가 transform 을 갱신 → prefab 의 NetworkTransform 가 다른 피어로 브로드캐스트 → 상대는 이
    /// 프록시를 원격 아바타로 관측. 비-owner 측에선 NetworkTransform 가 위치를 구동(관측). 로컬 플레이어
    /// (씬배치 싱글톤)는 *불변* — 프록시는 별 표현(client-local-authoritative). 단일플레이어 회귀 0.
    /// </summary>
    public class PlayerNetProxy : WMNetworkBehaviour
    {
        private void Update()
        {
            // 원격(비-owner) = prefab NetworkTransform 가 위치 구동 → 여기선 손 안 댐(경쟁 회피).
            if (IsOwner == false)
            {
                return;
            }

            // owner = 내 로컬 플레이어를 따라감. 로비/사망 등 플레이어 부재 시 probe=false → 유지.
            if (LocalPlayerProbeBridge.TryGetPosition(out float x, out float y, out float z))
            {
                transform.position = new Vector3(x, y, z);
            }
        }
    }
}
