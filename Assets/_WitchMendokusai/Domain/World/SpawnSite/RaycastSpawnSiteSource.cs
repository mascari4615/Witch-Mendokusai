using System;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public class RaycastSpawnSiteSettings
	{
		[SerializeField, Min(0f), Tooltip("기준 높이 위로 어디까지 보나 (m). 언덕 위 자리를 찾는 범위")] private float probeAbove = 8f;
		[SerializeField, Min(0f), Tooltip("기준 높이 아래로 어디까지 보나 (m). 내리막 자리를 찾는 범위")] private float probeBelow = 8f;
		[SerializeField, Min(0f), Tooltip("발밑 바닥을 찾을 때 기준 (몸 위치) 보다 이만큼 위의 면까지 발밑으로 침 (m). 경사와 계단 걸침")] private float footTolerance = .3f;
		[SerializeField, Min(0.01f), Tooltip("설 자리 머리 위를 보는 기둥 반지름 (m). 덮인 바닥만 잡게 가늘게. 몸 굵기로 하면 벽과 나무 옆 자리까지 버림")] private float headroomRadius = .1f;
		[SerializeField, Min(0.02f), Tooltip("설 자리 머리 위 공간 높이 (m). 이보다 낮은 틈 (언덕 밑에 깔린 바닥) 은 버림")] private float headroomHeight = 1.6f;
		[SerializeField, Min(0f), Tooltip("머리 위 공간 검사를 바닥에서 띄우는 틈 (m). 바닥 자체를 안 건드리게")] private float floorSkin = .05f;
		[SerializeField, Min(0f), Tooltip("공중 층 몬스터를 바닥 위 몇 m 에 놓나")] private float airHeight = 2.5f;
		[SerializeField, Tooltip("바닥으로 볼 충돌 층. 기본은 Ignore Raycast 를 뺀 전부. 장애물 (나무, 바위) 은 일부러 그 층이라 꼭대기를 바닥으로 안 잡음 (GroundObstacleField)")] private LayerMask groundMask = Physics.DefaultRaycastLayers;

		public float ProbeAbove => probeAbove;
		public float ProbeBelow => probeBelow;
		public float FootTolerance => footTolerance;
		public float HeadroomRadius => headroomRadius;
		public float HeadroomHeight => headroomHeight;
		public float FloorSkin => floorSkin;
		public float AirHeight => airHeight;
		public LayerMask GroundMask => groundMask;
	}

	// 위에서 아래로 쏴서 설 수 있는 면을 찾는 공급자. 평지와 언덕 판
	// 설 수 있는 면: 경사 한도 안, 트리거 아님, 다른 유닛 몸 아님 (Motor.Sweep 의 hitUnit 과 같은 판정), 머리 위가 안 덮임
	// 레이는 지나가는 충돌체를 다 맞히므로, 언덕 상자 밑에 깔린 바닥도 맞음. 머리 위 공간 검사가 그걸 버림
	// 발밑 질의 (AtOrBelowReference) 는 머리 위 검사 없음. 이미 몸이 선 자리이고, 맨 위 면을 고르니 덮인 바닥이 이길 일도 없음
	// 같은 충돌체는 머리 위 검사에서 뺌. 경사면이 제 몸에 걸리지 않게, 한 메시 안 동굴 바닥은 살게
	// 도달성 (절벽 아래, 막힌 방) 은 안 봄. 노드 그래프 공급자의 몫
	public class RaycastSpawnSiteSource : ISpawnSiteSource
	{
		private const int INITIAL_BUFFER_SIZE = 16;
		// 경사 한도는 Motor 기본값. 프리팹마다 바꾼 MotorTuning 은 안 따름 (스폰 시점에 몸이 정해지지 않음)
		private static readonly float WALKABLE_NORMAL_Y_MIN = new MotorTuning().GroundNormalYMin;

		private readonly RaycastSpawnSiteSettings settings;
		private RaycastHit[] hitBuffer = new RaycastHit[INITIAL_BUFFER_SIZE];
		private Collider[] overlapBuffer = new Collider[INITIAL_BUFFER_SIZE];

		public RaycastSpawnSiteSource(RaycastSpawnSiteSettings settings)
		{
			this.settings = settings;
		}

		public bool TryProject(in SpawnSiteQuery query, out SpawnSite site)
		{
			site = default;
			Vector3 origin = new(query.Candidate.x, query.ReferenceHeight + settings.ProbeAbove, query.Candidate.z);
			int hitCount = CastDown(origin, settings.ProbeAbove + settings.ProbeBelow);

			bool found = false;
			float bestScore = float.PositiveInfinity;
			for (int i = 0; i < hitCount; i++)
			{
				RaycastHit hit = hitBuffer[i];
				if (IsStandable(hit) == false)
					continue;
				if (TryScore(query, hit.point.y, out float score) == false || score >= bestScore)
					continue;
				if (query.Band == SpawnSiteBand.NearReference && HasHeadroom(hit) == false)
					continue;
				bestScore = score;
				site.Ground = hit.point;
				found = true;
			}
			if (found == false)
				return false;

			site.Position = query.Layer == SpawnLayer.Air ? site.Ground + Vector3.up * settings.AirHeight : site.Ground;
			return true;
		}

		// 작을수록 좋음
		private bool TryScore(in SpawnSiteQuery query, float height, out float score)
		{
			if (query.Band == SpawnSiteBand.AtOrBelowReference)
			{
				score = -height;
				return height <= query.ReferenceHeight + settings.FootTolerance;
			}
			score = Mathf.Abs(height - query.ReferenceHeight);
			return true;
		}

		private int CastDown(Vector3 origin, float distance)
		{
			int hitCount;
			do
			{
				hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, distance, settings.GroundMask, QueryTriggerInteraction.Ignore);
				if (hitCount < hitBuffer.Length)
					break;
				// 꽉 찬 결과는 순서 보장이 없어서 키워 다시 쏨 (Motor.RaycastOutsideBody 와 같은 처리)
				Array.Resize(ref hitBuffer, hitBuffer.Length * 2);
			} while (true);
			return hitCount;
		}

		private bool HasHeadroom(RaycastHit floor)
		{
			float radius = settings.HeadroomRadius;
			Vector3 bottom = floor.point + Vector3.up * (settings.FloorSkin + radius);
			Vector3 top = floor.point + Vector3.up * Mathf.Max(settings.FloorSkin + radius, settings.HeadroomHeight - radius);
			int overlapCount;
			do
			{
				overlapCount = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapBuffer, settings.GroundMask, QueryTriggerInteraction.Ignore);
				if (overlapCount < overlapBuffer.Length)
					break;
				Array.Resize(ref overlapBuffer, overlapBuffer.Length * 2);
			} while (true);

			for (int i = 0; i < overlapCount; i++)
			{
				Collider other = overlapBuffer[i];
				if (other == floor.collider || IsUnitBody(other.attachedRigidbody))
					continue;
				return false;
			}
			return true;
		}

		private static bool IsStandable(RaycastHit hit) =>
			hit.normal.y >= WALKABLE_NORMAL_Y_MIN && IsUnitBody(hit.rigidbody) == false;

		private static bool IsUnitBody(Rigidbody body) =>
			body != null && body.TryGetComponent(out UnitMovement _);
	}
}
