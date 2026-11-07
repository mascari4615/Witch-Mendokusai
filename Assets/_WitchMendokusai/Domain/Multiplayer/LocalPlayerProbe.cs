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

        private sealed class Probe : ILocalPlayerProbe, ILocalPlayerPull
        {
            // 로컬 Stage의 좌표 송신과 서버 보정 모두 차단. 귀환 시 현재 Stage 정책으로 복원
            private bool SharesPosition => StageManager.TryGetExistingInstance(out StageManager stages) &&
                stages.CurStage != null && stages.CurStage.ShareWorldPosition;

            /// <summary>
            /// 세계가 아는 자리로 옮긴다 (TASK-WM-217). <b>높이는 안 건드린다</b> —
            /// 세계는 y 를 모르고, 건드리면 땅에 박히거나 공중에 뜬다.
            /// </summary>
            public void PullTo(float x, float z)
            {
                if (SharesPosition == false)
                    return;
                if (PlayerProvider.TryGetExistingInstance(out PlayerProvider provider) == false)
                {
                    return;
                }
                PlayerObject playerObject = provider.CurrentObject;
                if (playerObject == null)
                {
                    return;
                }

                Vector3 position = playerObject.transform.position;
                // transform 만 쓰면 보간 Rigidbody 가 다음 프레임에 옛 자리로 되돌림 (2026-11-07 실측, 끌어당김과 placed 무효)
                playerObject.UnitMovement.Reposition(new Vector3(x, position.y, z));
            }

            public bool TryGetPose(out float x, out float y, out float z, out float yaw)
            {
                x = 0f;
                y = 0f;
                z = 0f;
                yaw = 0f;

                if (SharesPosition == false)
                    return false;

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
                // 인형 3D 모델(Mesh/Yawn2)이 이동방향으로 facing(PlayerRotation.meshPivotOf3DModel) — 그 yaw 동기.
                // 없으면 루트 yaw 폴백. 프록시 root 에 적용 → NetworkTransform 동기(스프라이트는 빌보드라 무관).
                Transform meshFacing = playerObject.transform.Find("Mesh/Yawn2");
                yaw = meshFacing != null ? meshFacing.eulerAngles.y : playerObject.transform.eulerAngles.y;
                return true;
            }
        }
    }
}
