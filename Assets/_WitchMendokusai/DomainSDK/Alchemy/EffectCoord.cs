namespace WitchMendokusai
{
	// TASK-WM-174 Phase 0 — 마도서 페이지의 목표 효과 좌표.
	// 솥 안 지도(효과 좌표계) 위 한 점 + 허용 도달 반경(Tolerance). 포션 마커가 Tolerance 안에 들어오면
	// 효과 달성. 강도/품질 정량화(거리에 따른 등급)는 Phase3 — Phase0 는 도달 boolean 만.
	// 순수 readonly struct — 캐릭터(링/알리사/욘)·서사 결합은 호출자 책임.
	public readonly struct EffectCoord
	{
		public readonly AlchemyVector Position;
		public readonly float Tolerance;

		public EffectCoord(AlchemyVector position, float tolerance)
		{
			Position = position;
			Tolerance = tolerance;
		}

		public bool ContainsArrival(AlchemyVector point)
		{
			return (point - Position).Magnitude <= Tolerance;
		}
	}
}
