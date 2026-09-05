using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 전술 룰의 타겟 선정 질의 — 진영 필터 + 우선순위 + 사거리.
	/// TargetingSystem(Domain) 이 이 POCO 를 해석해 후보를 정렬·선택한다.
	/// 순수 데이터(DomainSDK) — Domain 타입 의존 0.
	/// </summary>
	[Serializable]
	public struct TargetQuery
	{
		public TargetSide Side;
		public TargetPriority Priority;
		// 0 이하면 사거리 무제한.
		public float MaxRange;

		public TargetQuery(TargetSide side, TargetPriority priority, float maxRange = 0f)
		{
			Side = side;
			Priority = priority;
			MaxRange = maxRange;
		}
	}
}
