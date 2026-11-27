using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 자료 점검. 끊긴 id, 얻을 길 없는 물건, 어긋난 길이를 한 번에 모아 돌려줌
	public static partial class HomesteadCatalogCheck
	{
		public static List<string> Run(HomesteadCatalog catalog)
		{
			List<string> problems = new List<string>();
			CheckReferences(catalog, problems);
			CheckShapes(catalog, problems);
			CheckSources(catalog, problems);
			CheckLayout(catalog, problems);
			CheckCollection(catalog, problems);
			return problems;
		}

		private static void Need<T>(HomesteadCatalog catalog, string id, string owner, List<string> problems) where T : HomesteadDef
		{
			if (catalog.TryGet(id, out T _) == false)
			{
				problems.Add($"{owner}: {typeof(T).Name} '{id}' 없음");
			}
		}

		private static void NeedCost(HomesteadCatalog catalog, CostDef cost, string owner, List<string> problems)
		{
			foreach (string goods in cost.Goods.Keys)
			{
				Need<GoodsDef>(catalog, goods, owner, problems);
			}
		}

		private static void NeedReward(HomesteadCatalog catalog, RewardDef reward, string owner, List<string> problems)
		{
			foreach (string goods in reward.Goods.Keys)
			{
				Need<GoodsDef>(catalog, goods, owner, problems);
			}
			foreach (string furniture in reward.Furniture)
			{
				Need<FurnitureDef>(catalog, furniture, owner, problems);
			}
			foreach (string costume in reward.Costumes)
			{
				Need<CostumeDef>(catalog, costume, owner, problems);
			}
		}

		private static void NeedDrops(HomesteadCatalog catalog, List<DropDef> drops, string owner, List<string> problems)
		{
			foreach (DropDef drop in drops)
			{
				Need<GoodsDef>(catalog, drop.Goods, owner, problems);
				if (drop.Min > drop.Max || drop.Min < 1)
				{
					problems.Add($"{owner}: {drop.Goods} 수량 {drop.Min}..{drop.Max}");
				}
			}
		}

		// 오는 길의 열쇠가 실제로 일어날 수 있는 일을 가리키는지. 구출 열쇠는 던전의 '갇힌 사람' 이 그대로 돌려줌
		private static void NeedRecruitKey(HomesteadCatalog catalog, RecruitDef recruit, string owner, HashSet<string> rescueKeys, List<string> problems)
		{
			switch (recruit.Kind)
			{
				case RecruitKind.Rescue:
					if (HomesteadSim.IsRescueKey(recruit.Key) == false)
					{
						problems.Add($"{owner}: 구출 열쇠 '{recruit.Key}' 는 {HomesteadSim.RESCUE_PREFIX} 로 시작해야 함");
					}
					else if (rescueKeys.Add(recruit.Key) == false)
					{
						problems.Add($"{owner}: 구출 열쇠 '{recruit.Key}' 가 다른 주민과 겹침");
					}
					break;
				case RecruitKind.Event:
					if (recruit.Key != HomesteadSim.BOSS_EVENT)
					{
						problems.Add($"{owner}: 사건 열쇠 '{recruit.Key}' 를 알리는 곳이 없음");
					}
					break;
				case RecruitKind.Build:
					Need<BuildingDef>(catalog, recruit.Key, owner, problems);
					break;
			}
		}

		private static void CheckReferences(HomesteadCatalog catalog, List<string> problems)
		{
			foreach (CropDef crop in catalog.Crops)
			{
				Need<GoodsDef>(catalog, crop.Seed, crop.Id, problems);
				Need<GoodsDef>(catalog, crop.Harvest, crop.Id, problems);
			}
			foreach (GatherDef gather in catalog.Gathers)
			{
				NeedDrops(catalog, gather.Drops, gather.Id, problems);
			}
			foreach (FishDef fish in catalog.Fishes)
			{
				Need<GoodsDef>(catalog, fish.Goods, fish.Id, problems);
			}
			foreach (MineNodeDef node in catalog.MineNodes)
			{
				NeedDrops(catalog, node.Drops, node.Id, problems);
			}
			foreach (MineBandDef band in catalog.MineBands)
			{
				foreach (MineWeightDef weight in band.Weights)
				{
					Need<MineNodeDef>(catalog, weight.Node, band.Id, problems);
				}
			}
			foreach (RelicDef relic in catalog.Relics)
			{
				Need<GoodsDef>(catalog, relic.Goods, relic.Id, problems);
			}
			foreach (WingDef wing in catalog.Wings)
			{
				foreach (MilestoneDef milestone in wing.Milestones)
				{
					NeedReward(catalog, milestone.Reward, wing.Id, problems);
				}
			}
			foreach (ToolDef tool in catalog.Tools)
			{
				foreach (ToolTierDef tier in tool.Tiers)
				{
					NeedCost(catalog, tier.Cost, tool.Id, problems);
				}
			}
			foreach (TownHallDef hall in catalog.TownHalls)
			{
				NeedCost(catalog, hall.Cost, hall.Name, problems);
				NeedReward(catalog, hall.Reward, hall.Name, problems);
			}
			foreach (BuildingDef building in catalog.Buildings)
			{
				if (building.Resident.Length > 0)
				{
					Need<ResidentDef>(catalog, building.Resident, building.Id, problems);
				}
				foreach (BuildingLevelDef level in building.Levels)
				{
					NeedCost(catalog, level.Cost, building.Id, problems);
				}
			}
			HashSet<string> rescueKeys = new HashSet<string>();
			foreach (ResidentDef resident in catalog.Residents)
			{
				Need<BuildingDef>(catalog, resident.Building, resident.Id, problems);
				foreach (RecruitDef recruit in resident.Recruits)
				{
					NeedCost(catalog, recruit.Cost, resident.Id, problems);
					NeedRecruitKey(catalog, recruit, resident.Id, rescueKeys, problems);
				}
				foreach (RewardDef reward in resident.AffinityRewards)
				{
					NeedReward(catalog, reward, resident.Id, problems);
				}
				foreach (string favorite in resident.Favorites)
				{
					Need<GoodsDef>(catalog, favorite, resident.Id, problems);
				}
			}
			foreach (JobDef job in catalog.Jobs)
			{
				Need<BuildingDef>(catalog, job.Building, job.Id, problems);
				NeedDrops(catalog, job.Yields, job.Id, problems);
			}
			foreach (RecipeDef recipe in catalog.Recipes)
			{
				if (recipe.Building != HomesteadSim.BENCH)
				{
					Need<BuildingDef>(catalog, recipe.Building, recipe.Id, problems);
				}
				Need<GoodsDef>(catalog, recipe.Output, recipe.Id, problems);
				foreach (string input in recipe.Inputs.Keys)
				{
					Need<GoodsDef>(catalog, input, recipe.Id, problems);
				}
			}
			foreach (RequestDef request in catalog.Requests)
			{
				foreach (string goods in request.Goods)
				{
					Need<GoodsDef>(catalog, goods, request.Id, problems);
				}
			}
			foreach (FurnitureDef furniture in catalog.Furniture)
			{
				NeedCost(catalog, furniture.Cost, furniture.Id, problems);
			}
			foreach (CostumeDef costume in catalog.Costumes)
			{
				NeedCost(catalog, costume.Cost, costume.Id, problems);
			}
			foreach (AnimalDef animal in catalog.Animals)
			{
				Need<GoodsDef>(catalog, animal.Product, animal.Id, problems);
				Need<GoodsDef>(catalog, animal.FineProduct, animal.Id, problems);
			}
		}

		private static void CheckShapes(HomesteadCatalog catalog, List<string> problems)
		{
			for (int i = 0; i < catalog.TownHalls.Count; i++)
			{
				if (catalog.TownHalls[i].Tier != i + 1)
				{
					problems.Add($"타운홀 순서 {catalog.TownHalls[i].Tier}");
				}
			}
			for (int i = 0; i < catalog.HouseTiers.Count; i++)
			{
				if (catalog.HouseTiers[i].Tier != i + 1)
				{
					problems.Add($"집 순서 {catalog.HouseTiers[i].Tier}");
				}
			}
			foreach (ResidentDef resident in catalog.Residents)
			{
				if (resident.AffinitySteps.Count != resident.AffinityRewards.Count)
				{
					problems.Add($"{resident.Id}: 호감도 단계와 보상 수가 다름");
				}
			}
			foreach (RelicDef relic in catalog.Relics)
			{
				CheckRelic(catalog, relic, problems);
			}
			int depth = 1;
			foreach (MineBandDef band in catalog.MineBands)
			{
				if (band.From != depth)
				{
					problems.Add($"{band.Id}: 깊이가 이어지지 않음 {band.From}");
				}
				depth = band.To + 1;
			}
			if (catalog.Tuning.GuildSteps.Count < 2 || catalog.Tuning.ComfortSteps.Count < 2)
			{
				problems.Add("길드나 아늑함 단계표가 비었음");
			}
		}

		// 손질할 자리가 있고, 자리마다 맞는 도구가 있고, 뽑힐 수 있음
		private static void CheckRelic(HomesteadCatalog catalog, RelicDef relic, List<string> problems)
		{
			if (relic.Grime.Count == 0)
			{
				problems.Add($"{relic.Id}: 손질할 자리가 없음");
			}
			foreach (int grime in relic.Grime)
			{
				if (grime < 0 || grime >= RelicDef.TOOLS)
				{
					problems.Add($"{relic.Id}: 맞는 도구가 없는 때 {grime}");
				}
			}
			if (relic.Weight <= 0f)
			{
				problems.Add($"{relic.Id}: 무게 {relic.Weight} 라 나오지 않음");
			}
			if (catalog.TryGet(relic.Goods, out GoodsDef goods) && goods.Kind != GoodsKind.Relic)
			{
				problems.Add($"{relic.Id}: 물건 {relic.Goods} 가 유물 갈래가 아님");
			}
		}

		// 건물마다 배치 정의와 처음 자리가 있고, 처음 배치끼리 겹치지 않음
		private static void CheckLayout(HomesteadCatalog catalog, List<string> problems)
		{
			foreach (BuildingDef building in catalog.Buildings)
			{
				Need<PlaceableDef>(catalog, building.Id, "건물 자리", problems);
			}
			List<PlacedPiece> laid = new List<PlacedPiece>();
			foreach (PlacedPiece piece in VillageRules.Expand(catalog))
			{
				if (catalog.TryGet(piece.Id, out PlaceableDef placeable) == false)
				{
					problems.Add($"처음 배치: {piece.Id} 없음");
					continue;
				}
				if (VillageRules.IsInside(catalog.VillageArea, placeable, piece.X, piece.Z, piece.Turn) == false)
				{
					problems.Add($"처음 배치: {piece.Id} ({piece.X}, {piece.Z}) 마을 밖");
				}
				foreach (PlacedPiece other in laid)
				{
					if (VillageRules.Collides(placeable, piece, catalog.Get<PlaceableDef>(other.Id), other))
					{
						problems.Add($"처음 배치: {piece.Id} ({piece.X}, {piece.Z}) 와 {other.Id} ({other.X}, {other.Z}) 겹침");
					}
				}
				laid.Add(piece);
			}
		}

		// 값이나 조합에 쓰이는데 나오는 곳이 없는 물건. 던전 가방 물건은 밖에서 옴
		private static void CheckSources(HomesteadCatalog catalog, List<string> problems)
		{
			HashSet<string> sourced = new HashSet<string>();
			foreach (GoodsDef goods in catalog.Goods)
			{
				if (goods.Kind == GoodsKind.Material && goods.Note.Length > 0)
				{
					sourced.Add(goods.Id);
				}
			}
			foreach (CropDef crop in catalog.Crops)
			{
				sourced.Add(crop.Seed);
				sourced.Add(crop.Harvest);
			}
			foreach (GatherDef gather in catalog.Gathers)
			{
				gather.Drops.ForEach(drop => sourced.Add(drop.Goods));
			}
			catalog.Fishes.ForEach(fish => sourced.Add(fish.Goods));
			foreach (MineNodeDef node in catalog.MineNodes)
			{
				node.Drops.ForEach(drop => sourced.Add(drop.Goods));
			}
			catalog.Relics.ForEach(relic => sourced.Add(relic.Goods));
			foreach (AnimalDef animal in catalog.Animals)
			{
				sourced.Add(animal.Product);
				sourced.Add(animal.FineProduct);
			}
			catalog.Recipes.ForEach(recipe => sourced.Add(recipe.Output));
			foreach (GoodsDef goods in catalog.Goods)
			{
				if (sourced.Contains(goods.Id) == false)
				{
					problems.Add($"물건 {goods.Id} 나오는 곳 없음");
				}
			}
		}
	}
}
