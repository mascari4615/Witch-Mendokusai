using System.Collections.Generic;
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
        private const string MOVE_PARAM = "MOVE";
        private const float MOVE_SPEED_THRESHOLD = 0.3f; // m/s 이상 = 걷는 중(걷기 애니 ON).

        private Animator[] moveAnimators = System.Array.Empty<Animator>();
        private Vector3 lastPosition;
        private bool lastMoving;

        public override void OnStartClient()
        {
            base.OnStartClient();
            // owner 측 = 내 프록시 → 내 실 캐릭터 위에 겹침("메쉬 2개"). 프록시는 *남이 나를 보는* 표현이라
            // 나한텐 visual 숨김(SetActive 는 per-instance = 비동기 → 남들 화면엔 그대로 보임). TASK-WM-191.
            if (IsOwner)
            {
                Transform visual = transform.Find("DollVisual");
                if (visual != null)
                {
                    visual.gameObject.SetActive(false);
                }
            }

            // 걷기 애니('MOVE' bool) 구동할 인형 애니메이터 캐시. DollAnimator(로컬 플레이어 커플 → bake 시
            // 제거)가 원래 mainAnimator.SetBool("MOVE",isMoving) 했음 → 프록시선 위치델타로 직접 구동.
            // 'MOVE' 파라미터 있는 애니메이터만(없는 데 SetBool = warning 플러드 방지).
            List<Animator> withMove = new List<Animator>();
            foreach (Animator animator in GetComponentsInChildren<Animator>(true))
            {
                if (animator == null || animator.runtimeAnimatorController == null)
                {
                    continue;
                }
                foreach (AnimatorControllerParameter parameter in animator.parameters)
                {
                    if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == MOVE_PARAM)
                    {
                        withMove.Add(animator);
                        break;
                    }
                }
            }
            moveAnimators = withMove.ToArray();
            lastPosition = transform.position;
        }

        private void Update()
        {
            // owner = 내 로컬 플레이어 추종(위치 + facing yaw). 회전은 root → NetworkTransform(syncRotation)
            // 동기. 인형 3D 모델이 이동방향 facing, 스프라이트는 LookAtScreenCenter 빌보드(root 회전 무관).
            // 비-owner = NetworkTransform 가 위치/회전 구동(여기선 위치 안 건드림 = 경쟁 회피).
            if (IsOwner && LocalPlayerProbeBridge.TryGetPose(out float x, out float y, out float z, out float yaw))
            {
                transform.position = new Vector3(x, y, z);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            // 걷기 애니 — 동기된 위치 델타로 속도 파생(모든 피어 동일 판정, SyncVar 불요). 임계 초과 = 걷기 ON.
            Vector3 currentPosition = transform.position;
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float speed = (currentPosition - lastPosition).magnitude / deltaTime;
            lastPosition = currentPosition;
            bool moving = speed > MOVE_SPEED_THRESHOLD;
            if (moving != lastMoving)
            {
                lastMoving = moving;
                for (int index = 0; index < moveAnimators.Length; index++)
                {
                    if (moveAnimators[index] != null)
                    {
                        moveAnimators[index].SetBool(MOVE_PARAM, moving);
                    }
                }
            }
        }
    }
}
