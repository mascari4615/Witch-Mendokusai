using System.Collections.Generic;

namespace WitchMendokusai
{
	// 자원 흐름 한 줄 — 어떤 자원을 하루 얼마(rate) 만큼. 레시피 입/출력 + 생산결과 소비/산출 공용 단위.
	public readonly struct ResourceFlow
	{
		public readonly ResourceId Resource;
		public readonly float Rate;

		public ResourceFlow(ResourceId resource, float rate)
		{
			Resource = resource;
			Rate = rate;
		}
	}

	// 건물 1동의 생산 레시피 — 입력 자원들 + 노동력 소비 → 출력 자원들 (만가동 기준 rate). 다입력/다출력
	// 지원(크래프팅형 확장성). 입력·노동 0 이면 채취형(무에서 생산 = 외부 수출/원자재, 부트스트랩).
	// POCO(DomainSDK) — 게임 배선에선 Domain ProductionSO(DataSO)가 ZoneType/건물별 공급(스킨 deferred).
	public readonly struct ProductionRecipe
	{
		public readonly IReadOnlyList<ResourceFlow> Inputs;
		public readonly IReadOnlyList<ResourceFlow> Outputs;
		public readonly float LaborRequired; // 만가동(utilization 1)에 필요한 노동력(통근 노동자 수)

		public ProductionRecipe(IReadOnlyList<ResourceFlow> inputs, IReadOnlyList<ResourceFlow> outputs, float laborRequired)
		{
			Inputs = inputs;
			Outputs = outputs;
			LaborRequired = laborRequired;
		}
	}
}
