using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 솥 자료 검사. 물약 칸에 실제로 닿는지는 길 계산 (DomainSDK.Alchemy) 이 있어야 해서 시험 (HomesteadBrewTest) 몫
	public static partial class HomesteadCatalogCheck
	{
		// 흐린 물약은 하나. 물약은 Potion 이고 하나에 한 칸. 재료는 있는 물건이고 움직임이 0 이 아님
		private static void CheckBrews(HomesteadCatalog catalog, List<string> problems)
		{
			if (catalog.Brews.Count == 0)
			{
				return;
			}
			int fallbacks = catalog.Brews.FindAll(brew => brew.Fallback).Count;
			if (fallbacks != 1)
			{
				problems.Add($"흐린 물약 (Fallback) 이 {fallbacks} 개. 하나여야 함");
			}
			HashSet<string> outputs = new HashSet<string>();
			foreach (BrewDef brew in catalog.Brews)
			{
				Need<GoodsDef>(catalog, brew.Output, brew.Id, problems);
				if (outputs.Add(brew.Output) == false)
				{
					problems.Add($"{brew.Id}: 물약 '{brew.Output}' 의 칸이 둘");
				}
				if (catalog.TryGet(brew.Output, out GoodsDef output) && output.Kind != GoodsKind.Potion)
				{
					problems.Add($"{brew.Id}: '{brew.Output}' 은 Potion 이 아님");
				}
				if (brew.Value <= 0)
				{
					problems.Add($"{brew.Id}: 효과 값 {brew.Value}");
				}
				if (brew.Fallback == false && brew.Radius <= 0f)
				{
					problems.Add($"{brew.Id}: 칸 반지름 {brew.Radius}");
				}
			}
			foreach (BrewIngredientDef ingredient in catalog.BrewIngredients)
			{
				Need<GoodsDef>(catalog, ingredient.Id, "솥 재료", problems);
				if (ingredient.X == 0f && ingredient.Y == 0f)
				{
					problems.Add($"솥 재료 {ingredient.Id}: 움직임이 0");
				}
			}
			foreach (BrewHazardDef hazard in catalog.BrewHazards)
			{
				if (hazard.Radius <= 0f || hazard.Severity <= 0f)
				{
					problems.Add($"솥 얼룩 {hazard.Id}: 반지름 {hazard.Radius}, 세기 {hazard.Severity}");
				}
			}
		}
	}
}
