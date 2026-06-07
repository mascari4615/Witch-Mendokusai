using UnityEngine;
using WitchMendokusai.DomainSDK.Network;

namespace WitchMendokusai
{
    /// <summary>
    /// TASK-WM-191 step-1 — ILocalPlayerProbe 의 Domain impl. PlayerProvider(싱글톤 로컬 플레이어)의
    /// 위치를 노출해 WM.Network 프록시가 따라가게 한다. Domain↛Network 게이트 준수(seam 은 DomainSDK).
    /// 단일플레이어 회귀 0(읽기 전용 probe, 기존 플레이어 미변경).
    /// </summary>
    public static class LocalPlayerProbe
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            LocalPlayerProbeBridge.Register(new Probe());
        }

        private sealed class Probe : ILocalPlayerProbe
        {
            public bool TryGetPosition(out float x, out float y, out float z)
            {
                x = 0f;
                y = 0f;
                z = 0f;

                if (PlayerProvider.TryGetExistingInstance(out PlayerProvider provider) == false)
                {
                    return false;
                }
                PlayerObject playerObject = provider.CurrentObject;
                if (playerObject == null)
                {
                    return false;
                }

                Vector3 position = playerObject.transform.position;
                x = position.x;
                y = position.y;
                z = position.z;
                return true;
            }
        }
    }
}
