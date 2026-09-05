using System.Collections.Generic;

namespace WitchMendokusai
{
	// 생산 평가 결과 — 가동률 + 실제 소비/산출량(레시피 rate × utilization). 호출자(INC-5 일일 틱)가
	// Consumed 를 재고서 빼고 Produced 를 재고에 더한다. UtilizationRate = 0..1 (재고·노동 부족 시 비례 감산).
	public readonly struct ProductionResult
	{
		public readonly float UtilizationRate;
		public readonly IReadOnlyList<ResourceFlow> Consumed;
		public readonly IReadOnlyList<ResourceFlow> Produced;

		public ProductionResult(float utilizationRate, IReadOnlyList<ResourceFlow> consumed, IReadOnlyList<ResourceFlow> produced)
		{
			UtilizationRate = utilizationRate;
			Consumed = consumed;
			Produced = produced;
		}
	}
}
