using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 집 솥 자료 (brew.json) 를 솥 지도 (DomainSDK.Alchemy) 의 모양으로. 규칙 쪽 (HomesteadSim) 은 Alchemy 를 모름
	// 시험 HomesteadBrewTest.Walk 와 같은 변환 (함께 수정)
	public static class HomesteadBrewMap
	{
		public static BrewRecipe Recipe(BrewDef brew, string shownName) => new BrewRecipe
		{
			EffectName = shownName,
			Target = new EffectTarget { Position = new BrewVector(brew.X, brew.Y), Radius = brew.Radius },
		};

		// 재료의 (X, Y) 가 한 번 넣을 때의 움직임. 방향은 단위 벡터, 갈기는 길이
		public static CauldronMapElement.Ingredient Ingredient(BrewIngredientDef ingredient, string label)
		{
			BrewVector move = new BrewVector(ingredient.X, ingredient.Y);
			float length = move.Magnitude;
			if (length <= 0f)
				throw new System.ArgumentException($"솥 재료 {ingredient.Id} 의 움직임이 0. brew.json 확인");
			return new CauldronMapElement.Ingredient
			{
				Label = label,
				Direction = new BrewVector(move.X / length, move.Y / length),
				Grind = length,
			};
		}

		public static List<HazardZone> Hazards(HomesteadCatalog catalog)
		{
			List<HazardZone> hazards = new();
			for (int i = 0; i < catalog.BrewHazards.Count; i++)
			{
				BrewHazardDef hazard = catalog.BrewHazards[i];
				hazards.Add(new HazardZone
				{
					Id = i,
					Name = hazard.Name,
					Center = new BrewVector(hazard.X, hazard.Y),
					Radius = hazard.Radius,
					SeverityPerUnit = hazard.Severity,
				});
			}
			return hazards;
		}
	}
}
