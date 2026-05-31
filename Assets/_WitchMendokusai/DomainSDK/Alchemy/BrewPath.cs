using System.Collections.Generic;

namespace WitchMendokusai
{
	// TASK-WM-174 Phase 0 — 솥 안 지도 위 항해 자취.
	// 시작점(Origin)에서 출발, Apply(force) 호출마다 재료 벡터가 누적되어 끝점(Position)이 옮겨가고
	// waypoint 가 추가된다. 마도서 페이지(EffectCoord)에 도달했는지, 위험지대(HazardZone)를 몇 번
	// 관통했는지 순수 함수로 질의 — Phase1 솥 캔버스 UI 의 모델 정본.
	//
	// 결정성 계약: 같은 origin + 같은 force 시퀀스 → 같은 waypoints (EditMode 결정성 회귀 잠금).
	// 벡터 합성이 가환(+ 교환법칙)이므로 순서가 달라도 최종 Position 은 같다 — 다만 경로(중간
	// waypoints)는 달라 hazard 카운트가 다를 수 있다(같은 효과·다른 부작용의 디제틱 정합).
	//
	// 비전-중립: 재료가 마계 원소인지 / 캐릭터(링/알리사)가 누구 손인지 / 솥 비주얼은 스킨.
	// 모델은 2D 벡터 시퀀스만. 캐릭터 성향 모디파이어는 호출자가 force 벡터를 변환해 주입.
	public sealed class BrewPath
	{
		private readonly List<AlchemyVector> waypoints;

		public BrewPath(AlchemyVector origin)
		{
			waypoints = new List<AlchemyVector> { origin };
		}

		public IReadOnlyList<AlchemyVector> Waypoints => waypoints;

		public AlchemyVector Origin => waypoints[0];

		public AlchemyVector Position => waypoints[waypoints.Count - 1];

		public int StepCount => waypoints.Count - 1;

		public void Apply(AlchemyVector force)
		{
			AlchemyVector next = Position + force;
			waypoints.Add(next);
		}

		public bool HasArrived(EffectCoord target)
		{
			return target.ContainsArrival(Position);
		}

		// 자취가 위험지대를 관통한 선분 수. 같은 지대를 들락날락하면 다중 카운트 — 오래 머무를수록
		// 부작용 ↑ 의 디제틱 시그널(Phase3 정량화 시드). Phase0 은 횟수만 노출.
		public int CountHazardCrossings(HazardZone zone)
		{
			int crossings = 0;
			for (int i = 0; i < waypoints.Count - 1; i++)
			{
				if (zone.IntersectsSegment(waypoints[i], waypoints[i + 1]))
				{
					crossings++;
				}
			}
			return crossings;
		}
	}
}
