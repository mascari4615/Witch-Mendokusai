using System.Collections.Generic;

namespace WitchMendokusai
{
	// GlassBox 일일 시뮬 틱 — 생산 주문들을 CityEconomy 재고에 순차 적용(생산/소비). 순수(economy mutate만).
	//   (구조 리뷰의 CitySimulationSystem — CityGrowthSystem(성장 결정)의 형제, 경제 흐름 담당.)
	//
	// 주문 순서 = 공급망 순서(예: 산업 원자재 → 상업 가공 → 주거 소비) — 앞 주문 산출을 뒤 주문이 같은 틱에 소비.
	// 노동은 주문들이 공유하는 풀(앞 주문이 가동률만큼 소진) — 단순 모델, 공간 통근(INC-7)은 별개 레이어.
	public sealed class CitySimulationSystem
	{
		private readonly BuildingProductionModel productionModel = new();

		public void RunDay(CityEconomy economy, IReadOnlyList<ProductionOrder> orders, float availableLabor)
		{
			float remainingLabor = availableLabor;

			foreach (ProductionOrder order in orders)
			{
				if (order.Count <= 0)
				{
					continue;
				}

				ProductionRecipe scaled = ScaleRecipe(order.Recipe, order.Count);
				ProductionResult result = productionModel.Evaluate(scaled, economy.Stock, remainingLabor);

				foreach (ResourceFlow consumed in result.Consumed)
				{
					economy.AddStock(consumed.Resource, -consumed.Rate);
				}

				foreach (ResourceFlow produced in result.Produced)
				{
					economy.AddStock(produced.Resource, produced.Rate);
				}

				remainingLabor -= result.UtilizationRate * scaled.LaborRequired;
				if (remainingLabor < 0f)
				{
					remainingLabor = 0f;
				}
			}
		}

		// 레시피 × 건물 수 — 입출력 rate·노동 요구를 N배. count==1 이면 원본 그대로.
		private static ProductionRecipe ScaleRecipe(ProductionRecipe recipe, int count)
		{
			if (count == 1)
			{
				return recipe;
			}

			List<ResourceFlow> inputs = new(recipe.Inputs.Count);
			foreach (ResourceFlow flow in recipe.Inputs)
			{
				inputs.Add(new ResourceFlow(flow.Resource, flow.Rate * count));
			}

			List<ResourceFlow> outputs = new(recipe.Outputs.Count);
			foreach (ResourceFlow flow in recipe.Outputs)
			{
				outputs.Add(new ResourceFlow(flow.Resource, flow.Rate * count));
			}

			return new ProductionRecipe(inputs, outputs, recipe.LaborRequired * count);
		}
	}
}
