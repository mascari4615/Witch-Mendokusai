using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// GlassBox 생산/소비 — 순수 함수(상태 0, new() 후 Evaluate 만). RciDemandModel 동형 형제.
	//
	//  가동률 utilization = min(1, 노동가용/노동요구, 모든 입력에 대해 재고/요구) → clamp[0,1].
	//  소비 = 입력 rate × utilization / 산출 = 출력 rate × utilization. 가장 부족한 자원·노동이 병목.
	//  입력·노동 0 레시피 = 무에서 생산(채취/수출형, utilization 1) — 도시 경제 부트스트랩(산업 exportBaseline 대응).
	//
	// 비전-중립 — 어떤 건물이 무슨 자원을 만드는지(ResourceId 의미)는 Domain ResourceSO/ProductionSO 가
	// 추후 부여(스킨 deferred). 모델은 id+rate 산술만. 계수·레시피 = 주입(수치 노출, DataSO 공급).
	public sealed class BuildingProductionModel
	{
		private const float UTILIZATION_MIN = 0f;
		private const float UTILIZATION_MAX = 1f;

		public ProductionResult Evaluate(ProductionRecipe recipe, IReadOnlyDictionary<ResourceId, float> availableStock, float availableLabor)
		{
			float utilization = UTILIZATION_MAX;

			// 노동 병목 — 만가동에 LaborRequired 필요, 가용이 모자라면 비례 감산.
			if (recipe.LaborRequired > 0f)
			{
				utilization = Mathf.Min(utilization, availableLabor / recipe.LaborRequired);
			}

			// 입력 재고 병목 — 가장 부족한 입력이 가동률 상한.
			foreach (ResourceFlow input in recipe.Inputs)
			{
				if (input.Rate <= 0f)
				{
					continue;
				}

				float have = availableStock.TryGetValue(input.Resource, out float stocked) ? stocked : 0f;
				utilization = Mathf.Min(utilization, have / input.Rate);
			}

			utilization = Mathf.Clamp(utilization, UTILIZATION_MIN, UTILIZATION_MAX);

			List<ResourceFlow> consumed = ScaleFlows(recipe.Inputs, utilization);
			List<ResourceFlow> produced = ScaleFlows(recipe.Outputs, utilization);

			return new ProductionResult(utilization, consumed, produced);
		}

		private static List<ResourceFlow> ScaleFlows(IReadOnlyList<ResourceFlow> flows, float utilization)
		{
			List<ResourceFlow> scaled = new(flows.Count);
			foreach (ResourceFlow flow in flows)
			{
				scaled.Add(new ResourceFlow(flow.Resource, flow.Rate * utilization));
			}

			return scaled;
		}
	}
}
