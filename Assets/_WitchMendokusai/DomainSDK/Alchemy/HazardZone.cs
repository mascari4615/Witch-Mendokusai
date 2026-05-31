using UnityEngine;

namespace WitchMendokusai
{
	// TASK-WM-174 Phase 0 — 솥 지도 위 저주 폭주 위험지대(원형).
	// 경로 선분이 이 영역을 관통하면 부작용 플래그 → BrewPath.CountHazardCrossings 가 누적 횟수 반환.
	// 부작용 정량화(강도·종류·링/알리사 성향 모디파이어)는 Phase3 — Phase0 는 관통 boolean/카운트만.
	//
	// IntersectsSegment: 점-선분 최단거리 ≤ Radius. 시작/끝 안쪽 또는 외부에서 살짝 스쳐도 true.
	// "잠깐 들어왔다 나가도 관통" 의 디제틱 의미와 정합 — 솥 안 위험은 한 번 닿으면 영향.
	public readonly struct HazardZone
	{
		public readonly AlchemyVector Center;
		public readonly float Radius;

		public HazardZone(AlchemyVector center, float radius)
		{
			Center = center;
			Radius = radius;
		}

		public bool Contains(AlchemyVector point)
		{
			return (point - Center).Magnitude <= Radius;
		}

		public bool IntersectsSegment(AlchemyVector from, AlchemyVector to)
		{
			AlchemyVector segment = to - from;
			float lengthSquared = segment.X * segment.X + segment.Y * segment.Y;

			if (lengthSquared == 0f)
			{
				return Contains(from);
			}

			AlchemyVector centerOffset = Center - from;
			float projection = (centerOffset.X * segment.X + centerOffset.Y * segment.Y) / lengthSquared;
			float clamped = Mathf.Clamp01(projection);

			AlchemyVector closest = new AlchemyVector(
				from.X + segment.X * clamped,
				from.Y + segment.Y * clamped);

			return (closest - Center).Magnitude <= Radius;
		}
	}
}
