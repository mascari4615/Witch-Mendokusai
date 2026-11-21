using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 예고 쪽 부품. 발밑 방향 실루엣, 다음 자리 바닥 예고
	public partial class Spawner
	{
		[Header("Preview")]
		[SerializeField, Tooltip("다음 공격 모양을 발밑에 까는 실루엣. 비우면 없음. 판정은 BoxCollider 기준")] private AimSilhouette silhouettePrefab;
		[SerializeField, Tooltip("다음 자리를 미리 보이는 바닥 예고 (원형). 비우면 없음")] private CombatFx nextSpotTelegraph;
		[SerializeField, Min(0f), Tooltip("예고 원 반지름 (월드 단위). 범위 강화를 같이 받음")] private float telegraphRadius = 1.5f;
		[SerializeField, Min(0.05f), Tooltip("발사 몇 초 전에 보일지")] private float telegraphLeadSeconds = 1f;

		private readonly List<AimSilhouette> silhouettes = new();
		private readonly List<Action> telegraphStops = new();
		private SwordSwingArea footprint;
		private bool hasMeasuredFootprint;

		private bool HasNextSpotTelegraph => nextSpotTelegraph != null && telegraphRadius > 0f;

		private float FootprintScale() => playerProvider.Current.UnitStat.PercentMultiplier(UnitStatType.PROJECTILE_SCALE_BONUS) * ScaleMultiplier();

		private void EnablePreview()
		{
			moveAim.Reset();
			if (silhouettePrefab == null || prefabToSpawn == null)
				return;
			if (playerProvider.HasPlayer)
				TickMoveAim(0f);
			footprint = SwordSwingArea.FromHitbox(prefabToSpawn.transform);
			hasMeasuredFootprint = true;
			int count = spawnOpposite ? 2 : 1;
			for (int i = 0; i < count; i++)
				silhouettes.Add(Instantiate(silhouettePrefab));
		}

		private void DisablePreview()
		{
			foreach (AimSilhouette silhouette in silhouettes)
				if (silhouette != null)
					Destroy(silhouette.gameObject);
			silhouettes.Clear();
			foreach (Action stop in telegraphStops)
				stop();
			telegraphStops.Clear();
		}

		private void LateUpdate()
		{
			if (silhouettes.Count == 0 || hasMeasuredFootprint == false || playerProvider.HasPlayer == false)
				return;
			bool hasDirection = TryGetPreviewDirection(out Vector3 direction);
			float scale = FootprintScale();
			Vector3 origin = playerProvider.Current.transform.position;
			for (int i = 0; i < silhouettes.Count; i++)
			{
				silhouettes[i].SetFootprint(footprint.MinX * scale, footprint.MaxX * scale, footprint.MinZ * scale, footprint.MaxZ * scale);
				if (hasDirection)
					silhouettes[i].Show(origin, i == 0 ? direction : -direction);
				else
					silhouettes[i].Hide();
			}
		}

		// 다음 발이 향할 방향. 낼 때 정해지는 조준 (가장 가까운 적 등) 은 미리 알 수 없어 보이지 않음
		private bool TryGetPreviewDirection(out Vector3 direction)
		{
			direction = Vector3.zero;
			if (spawnPositionStrategy != SpawnPositionStrategy.Player)
				return false;
			switch (spawnAimStrategy)
			{
				case SpawnAimStrategy.LastMoveDirection:
					direction = reverseAim ? -moveAim.Current : moveAim.Current;
					break;
				case SpawnAimStrategy.Aim:
					direction = playerProvider.Current.AimDirection;
					break;
				case SpawnAimStrategy.Clock:
					direction = Quaternion.Euler(0f, clockAngle, 0f) * Vector3.forward;
					break;
				default:
					return false;
			}
			direction.y = 0f;
			if (direction.sqrMagnitude < 0.0001f)
				return false;
			direction = Quaternion.Euler(0f, angleCursor, 0f) * direction.normalized;
			return true;
		}

		// 계획한 자리마다 원형 예고. 뒤에 발사되는 발은 SpawnDelay 만큼 더 오래 보임
		private void ShowNextSpotTelegraph(ShotPlan[] plan, float leadSeconds)
		{
			telegraphStops.Clear();
			float radius = telegraphRadius * FootprintScale();
			for (int i = 0; i < plan.Length; i++)
			{
				if (plan[i].HasPosition == false)
					continue;
				telegraphStops.Add(nextSpotTelegraph.Play(new CombatFxContext
				{
					Position = plan[i].Position,
					Direction = Vector3.forward,
					Radius = radius,
					Seconds = leadSeconds + SpawnDelay * i,
				}));
			}
		}
	}
}
