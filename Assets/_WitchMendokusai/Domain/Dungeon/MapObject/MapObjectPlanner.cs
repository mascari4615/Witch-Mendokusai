using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public readonly struct MapObjectPlan
	{
		public readonly MapObjectData Data;
		public readonly float SpawnTime;
		// 뽑힌 순서. 같은 시각이면 이 순서로 배치
		public readonly int Order;

		public MapObjectPlan(MapObjectData data, float spawnTime, int order)
		{
			Data = data;
			SpawnTime = spawnTime;
			Order = order;
		}
	}

	public enum MapObjectProbeResult
	{
		Ok = 0,
		NoGround = 1,
		Blocked = 2,
		RouteBlocked = 3,
	}

	// 후보 자리 하나의 물리 검사. 시험에서는 가짜로 바꿈
	public delegate MapObjectProbeResult MapObjectProbe(Vector3 candidate, out Vector3 ground);

	// 자리 고르기 실패 원인별 수. 진단 로그와 개발자 창
	public struct MapObjectPlacementTally
	{
		public int Attempts;
		public int Spacing;
		public int NoGround;
		public int Blocked;
		public int RouteBlocked;

		public override string ToString() => $"시도 {Attempts}, 간격 {Spacing}, 지면 없음 {NoGround}, 막힘 {Blocked}, 길 막힘 {RouteBlocked}";
	}

	// 등장 빈도 규칙 (MapObjectSettings). 체감 빈도를 예산과 따로 조절
	public readonly struct MapObjectPacing
	{
		// 앞 오브젝트와 등장 시각 최소 간격 (초). 0 이면 계획 시각 그대로
		public readonly float GapSeconds;
		// 가중치 뽑기에서 바로 앞과 같은 종류를 피함 (다른 후보가 있을 때)
		public readonly bool AvoidRepeat;

		public static readonly MapObjectPacing None = new(0f, false);

		public MapObjectPacing(float gapSeconds, bool avoidRepeat)
		{
			GapSeconds = gapSeconds;
			AvoidRepeat = avoidRepeat;
		}
	}

	// 판 시작 배치 예산 계산. 엔진 물리 없음, 같은 시드면 같은 결과
	public static class MapObjectPlanner
	{
		// 1. 최소 개수를 후보 순서대로 예산 안에서 채움
		// 2. 남은 크레딧으로 살 수 있고 상한이 남은 후보를 가중치로 뽑기, 살 것이 없을 때까지. 바로 앞과 같은 종류는 피함 (pacing)
		// 3. 시각순으로 놓고 앞 오브젝트와 간격이 GapSeconds 보다 좁으면 뒤로 미룸
		public static List<MapObjectPlan> Plan(MapObjectBudget budget, int seed, MapObjectPacing pacing, out int remainingCredits)
		{
			System.Random random = new(seed);
			Dictionary<MapObjectData, int> counts = new();
			List<MapObjectPlan> plans = new();
			int remaining = budget.Credits;

			foreach (MapObjectData candidate in budget.Candidates)
			{
				while (Count(counts, candidate) < candidate.CountRange.x && candidate.PlacementCost <= remaining)
					Buy(candidate, random, counts, plans, ref remaining);
			}

			List<MapObjectData> affordable = new();
			MapObjectData previous = null;
			for (int pick = 0; pick < budget.MaxPicks; pick++)
			{
				affordable.Clear();
				float totalWeight = 0f;
				foreach (MapObjectData candidate in budget.Candidates)
				{
					if (candidate.Weight <= 0f || candidate.PlacementCost > remaining || Count(counts, candidate) >= candidate.CountRange.y)
						continue;
					affordable.Add(candidate);
					totalWeight += candidate.Weight;
				}
				if (affordable.Count == 0)
					break;
				if (pacing.AvoidRepeat && affordable.Count > 1 && affordable.Remove(previous))
					totalWeight -= previous.Weight;
				previous = PickWeighted(affordable, totalWeight, random);
				Buy(previous, random, counts, plans, ref remaining);
			}

			plans.Sort((left, right) =>
			{
				int byTime = left.SpawnTime.CompareTo(right.SpawnTime);
				return byTime != 0 ? byTime : left.Order.CompareTo(right.Order);
			});
			for (int i = 1; i < plans.Count; i++)
			{
				float earliest = plans[i - 1].SpawnTime + pacing.GapSeconds;
				if (plans[i].SpawnTime < earliest)
					plans[i] = new MapObjectPlan(plans[i].Data, earliest, plans[i].Order);
			}
			remainingCredits = remaining;
			return plans;
		}

		private static int Count(Dictionary<MapObjectData, int> counts, MapObjectData data) => counts.TryGetValue(data, out int count) ? count : 0;

		private static void Buy(MapObjectData data, System.Random random, Dictionary<MapObjectData, int> counts, List<MapObjectPlan> plans, ref int remaining)
		{
			remaining -= data.PlacementCost;
			counts[data] = Count(counts, data) + 1;
			plans.Add(new MapObjectPlan(data, RangeValue(random, data.SpawnTime.x, data.SpawnTime.y), plans.Count));
		}

		private static MapObjectData PickWeighted(List<MapObjectData> candidates, float totalWeight, System.Random random)
		{
			float roll = (float)random.NextDouble() * totalWeight;
			foreach (MapObjectData candidate in candidates)
			{
				roll -= candidate.Weight;
				if (roll < 0f)
					return candidate;
			}
			return candidates[candidates.Count - 1];
		}

		private static float RangeValue(System.Random random, float min, float max) => max > min ? min + (float)random.NextDouble() * (max - min) : min;

		// 주인공 둘레 거리 범위의 무작위 자리. 다른 오브젝트 간격 (같은 종류는 sameKindSpacing) 을 먼저 보고 물리 검사는 그다음
		public static bool TryPickPosition(System.Random random, MapObjectData data, Vector3 origin, IReadOnlyList<Vector3> placed, IReadOnlyList<Vector3> sameKind, float sameKindSpacing, int attempts, MapObjectProbe probe, out Vector3 position, out MapObjectPlacementTally tally)
		{
			tally = new MapObjectPlacementTally();
			float spacingSquared = data.MinSpacing * data.MinSpacing;
			float sameKindSquared = sameKindSpacing * sameKindSpacing;
			for (int attempt = 0; attempt < attempts; attempt++)
			{
				tally.Attempts++;
				float angle = (float)(random.NextDouble() * Math.PI * 2.0);
				float distance = RangeValue(random, data.PlayerDistance.x, data.PlayerDistance.y);
				Vector3 candidate = origin + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
				if (IsCrowded(candidate, placed, spacingSquared) || IsCrowded(candidate, sameKind, sameKindSquared))
				{
					tally.Spacing++;
					continue;
				}
				MapObjectProbeResult result = probe(candidate, out Vector3 ground);
				switch (result)
				{
					case MapObjectProbeResult.Ok:
						position = ground;
						return true;
					case MapObjectProbeResult.NoGround:
						tally.NoGround++;
						break;
					case MapObjectProbeResult.Blocked:
						tally.Blocked++;
						break;
					case MapObjectProbeResult.RouteBlocked:
						tally.RouteBlocked++;
						break;
				}
			}
			position = origin;
			return false;
		}

		private static bool IsCrowded(Vector3 candidate, IReadOnlyList<Vector3> placed, float spacingSquared)
		{
			foreach (Vector3 other in placed)
			{
				Vector3 offset = other - candidate;
				offset.y = 0f;
				if (offset.sqrMagnitude < spacingSquared)
					return true;
			}
			return false;
		}
	}
}
