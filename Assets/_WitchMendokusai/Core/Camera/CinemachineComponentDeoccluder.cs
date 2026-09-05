using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 컴포넌트 기반 카메라 가림 해소 Extension — CinemachineDeoccluder 정본의 LayerMask
	/// 식별 대신 GroundSurface marker (땅·벽 등 「물리적으로 통과 못 하는 표면」 family)
	/// 가진 collider 만 「가림」 으로 인정.
	/// Physics Layer 슬롯(32개 한정) 점유 회피 + 점프 판정과 짝 — 「밟는 표면 = 카메라 가림 표면」.
	///
	/// 동작: Body stage 후 LookAt → RawPosition 방향으로 SphereCast → GroundSurface
	/// 가진 collider 중 가장 가까운 hit 보다 카메라가 멀면 그 hit 지점으로 당김.
	/// damping (당길 때) + smoothingTime (풀려서 멀어질 때) 으로 jitter 방지. TASK-WM-161.
	/// </summary>
	[AddComponentMenu("Cinemachine/Extensions/WM Component Deoccluder")]
	[SaveDuringPlay]
	public class CinemachineComponentDeoccluder : CinemachineExtension
	{
		[Tooltip("Broad-phase Physics 검사 레이어 — Everything 그대로 두고 GroundSurface 컴포넌트로 최종 필터")]
		[SerializeField] private LayerMask broadMask = ~0;

		[Tooltip("카메라를 구체로 간주 — 벽에 살짝 띄움. 작을수록 모서리 스침 둔감")]
		[Range(0.01f, 1f)]
		[SerializeField] private float cameraRadius = 0.08f;

		[Tooltip("LookAt 와 카메라 사이 최소 거리 — 너무 가까이 당기지 X")]
		[SerializeField] private float minimumDistance = 0.5f;

		[Tooltip("trigger collider 도 가림으로 인정할지")]
		[SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

		[Tooltip("당겨질 때 부드러움 (초) — 0=즉시. 0.1~0.3 추천")]
		[Range(0f, 1f)]
		[SerializeField] private float damping = 0.15f;

		[Tooltip("가림 풀려 멀어질 때 부드러움 (초) — 보통 damping 보다 더 김. 0.3~0.8 추천")]
		[Range(0f, 2f)]
		[SerializeField] private float smoothingTime = 0.5f;

		[Tooltip("장애물이 이 시간(초) 이상 지속 가려야 카메라를 당김. 빠르게 이동하며 스쳐가는 순간 장애물(기둥·벽)은 무시 → 카메라 거리 안 흔들려 어지러움 ↓. 0 = 즉시(구 동작). 0.05~0.15 추천. TASK-WM-163")]
		[Range(0f, 0.5f)]
		[SerializeField] private float occlusionPersistTime = 0.08f;

		[Header("Debug")]
		[Tooltip("Scene view 에 카메라-LookAt 선·hit 지점·구체 표시. Play 중 Scene view 에서 보임. Game view 도 보고 싶으면 Game 탭 상단 Gizmos 토글 ON.")]
		[SerializeField] private bool showDebugGizmos = false;

		[Tooltip("hit 지점 구체 색")]
		[SerializeField] private Color hitColor = new(1f, 0.3f, 0.2f, 0.9f);

		[Tooltip("미가림 카메라 광선 색")]
		[SerializeField] private Color clearColor = new(0.3f, 1f, 0.4f, 0.6f);

		private const int HIT_BUFFER_SIZE = 16;
		private static readonly RaycastHit[] HIT_BUFFER = new RaycastHit[HIT_BUFFER_SIZE];

		private readonly Dictionary<CinemachineVirtualCameraBase, VcamState> stateByVcam = new();

		private struct VcamState
		{
			public float currentDistance;
			public float velocity;
			public float occludedTime; // 현재 가림이 지속된 시간 (persist 게이트용)

			// Debug 캐시 — OnDrawGizmos 가 Body callback 안에서 그릴 수 없어 마지막 값 보관
			public Vector3 debugTarget;
			public Vector3 debugDirection;
			public float debugDesiredDistance;
			public float debugOccludedDistance;
			public bool debugWasOccluded;
		}

		protected override void PostPipelineStageCallback(
			CinemachineVirtualCameraBase vcam,
			CinemachineCore.Stage stage,
			ref CameraState state,
			float deltaTime)
		{
			if (stage != CinemachineCore.Stage.Body)
				return;

			if (state.HasLookAt() == false)
				return;

			Vector3 target = state.ReferenceLookAt;
			Vector3 delta = state.RawPosition - target;
			float desiredDistance = delta.magnitude;
			if (desiredDistance < 0.0001f)
				return;

			Vector3 direction = delta / desiredDistance;

			int hitCount = Physics.SphereCastNonAlloc(
				target,
				cameraRadius,
				direction,
				HIT_BUFFER,
				desiredDistance,
				broadMask,
				triggerInteraction);

			float occludedDistance = desiredDistance;
			for (int i = 0; i < hitCount; i++)
			{
				if (HIT_BUFFER[i].collider.GetComponentInParent<GroundSurface>() == null)
					continue;

				if (HIT_BUFFER[i].distance < occludedDistance)
					occludedDistance = HIT_BUFFER[i].distance;
			}

			if (stateByVcam.TryGetValue(vcam, out VcamState vcamState) == false)
			{
				vcamState = new VcamState { currentDistance = desiredDistance, velocity = 0f, occludedTime = 0f };
			}

			// WM-163 폴리싱 — 빠른 이동 중 스쳐가는 순간 장애물 무시. occlusion 이 persist 시간
			// 넘게 지속돼야 카메라를 당긴다 → 달리며 지나치는 기둥/벽이 카메라 거리를 안 흔듦 (어지러움 ↓).
			bool isOccluded = occludedDistance < desiredDistance - 0.01f;
			vcamState.occludedTime = isOccluded ? vcamState.occludedTime + deltaTime : 0f;
			bool committedOcclusion = isOccluded && vcamState.occludedTime >= occlusionPersistTime;
			float targetDistance = committedOcclusion ? Mathf.Max(minimumDistance, occludedDistance) : desiredDistance;

			// 당기는 방향(가까워짐) = damping / 풀리는 방향(멀어짐) = smoothingTime
			bool pullingIn = targetDistance < vcamState.currentDistance;
			float smoothTime = pullingIn ? damping : smoothingTime;

			if (deltaTime > 0f && smoothTime > 0.0001f)
			{
				vcamState.currentDistance = Mathf.SmoothDamp(
					vcamState.currentDistance,
					targetDistance,
					ref vcamState.velocity,
					smoothTime,
					Mathf.Infinity,
					deltaTime);
			}
			else
			{
				vcamState.currentDistance = targetDistance;
				vcamState.velocity = 0f;
			}

			vcamState.debugTarget = target;
			vcamState.debugDirection = direction;
			vcamState.debugDesiredDistance = desiredDistance;
			vcamState.debugOccludedDistance = occludedDistance;
			vcamState.debugWasOccluded = occludedDistance < desiredDistance;

			stateByVcam[vcam] = vcamState;

			state.RawPosition = target + (direction * vcamState.currentDistance);

			if (showDebugGizmos)
			{
				Color rayColor = vcamState.debugWasOccluded ? hitColor : clearColor;
				// LookAt → 원래 의도 위치 (어두운 색 — "원거리 의도")
				Debug.DrawLine(target, target + (direction * desiredDistance), rayColor * 0.4f);
				// LookAt → 실제 카메라 위치 (밝은 색 — "현 carbon")
				Debug.DrawLine(target, target + (direction * vcamState.currentDistance), rayColor);
				if (vcamState.debugWasOccluded)
				{
					Vector3 hitPoint = target + (direction * occludedDistance);
					Debug.DrawLine(hitPoint + Vector3.up * 0.2f, hitPoint - Vector3.up * 0.2f, hitColor);
					Debug.DrawLine(hitPoint + Vector3.right * 0.2f, hitPoint - Vector3.right * 0.2f, hitColor);
					Debug.DrawLine(hitPoint + Vector3.forward * 0.2f, hitPoint - Vector3.forward * 0.2f, hitColor);
				}
			}
		}

		private void OnDrawGizmos()
		{
			if (showDebugGizmos == false)
				return;

			foreach (KeyValuePair<CinemachineVirtualCameraBase, VcamState> kv in stateByVcam)
			{
				VcamState s = kv.Value;
				Vector3 currentPos = s.debugTarget + (s.debugDirection * s.currentDistance);
				Vector3 desiredPos = s.debugTarget + (s.debugDirection * s.debugDesiredDistance);

				// 현재 카메라 위치 구체 (radius 시각화)
				Gizmos.color = s.debugWasOccluded ? hitColor : clearColor;
				Gizmos.DrawWireSphere(currentPos, cameraRadius);

				// 원래 의도 위치 구체 (어둡게)
				Gizmos.color = clearColor * 0.4f;
				Gizmos.DrawWireSphere(desiredPos, cameraRadius * 0.6f);

				// hit 지점 구체
				if (s.debugWasOccluded)
				{
					Vector3 hitPos = s.debugTarget + (s.debugDirection * s.debugOccludedDistance);
					Gizmos.color = hitColor;
					Gizmos.DrawWireSphere(hitPos, cameraRadius * 0.8f);
				}
			}
		}

		protected override void OnDestroy()
		{
			stateByVcam.Clear();
			base.OnDestroy();
		}
	}
}
