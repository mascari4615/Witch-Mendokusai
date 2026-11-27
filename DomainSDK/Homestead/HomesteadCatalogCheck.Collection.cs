using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 수집 쪽 점검. 관에 걸 물건마다 열리는 가장 이른 타운홀, 이정표가 받침대 수 안인지
	public static partial class HomesteadCatalogCheck
	{
		public const int NEVER = int.MaxValue;

		// 얻는 길 중 가장 일찍 열리는 타운홀. 길이 없거나 모두 닫혀 있으면 NEVER
		// 문은 작물의 타운홀, 도구 등급의 타운홀, 박물관 (유물 손질), 목장 단계. 던전 가방에서 오는 재료는 셈 밖
		public static int FirstTownHall(HomesteadCatalog catalog, string goodsId)
		{
			int best = NEVER;
			foreach (CropDef crop in catalog.Crops)
			{
				if (crop.Harvest == goodsId || crop.Seed == goodsId)
				{
					best = Math.Min(best, crop.TownHall);
				}
			}
			foreach (GatherDef gather in catalog.Gathers)
			{
				if (gather.Drops.Exists(drop => drop.Goods == goodsId))
				{
					best = Math.Min(best, ToolTownHall(catalog, gather.Tool, gather.Tier));
				}
			}
			foreach (FishDef fish in catalog.Fishes)
			{
				if (fish.Goods == goodsId && fish.Waters.Count > 0 && fish.Weight > 0f && fish.HourFrom != fish.HourTo)
				{
					best = Math.Min(best, ToolTownHall(catalog, ToolKind.Rod, fish.RodTier));
				}
			}
			foreach (MineNodeDef node in catalog.MineNodes)
			{
				if (node.Drops.Exists(drop => drop.Goods == goodsId) && IsInAnyBand(catalog, node.Id))
				{
					best = Math.Min(best, ToolTownHall(catalog, ToolKind.Pickaxe, node.Tier));
				}
			}
			foreach (RelicDef relic in catalog.Relics)
			{
				if (relic.Goods == goodsId && relic.Weight > 0f && HasRelicSource(catalog))
				{
					best = Math.Min(best, LevelTownHall(catalog, HomesteadSim.MUSEUM, 1));
				}
			}
			foreach (AnimalDef animal in catalog.Animals)
			{
				if (animal.Product == goodsId || animal.FineProduct == goodsId)
				{
					best = Math.Min(best, LevelTownHall(catalog, HomesteadSim.RANCH, animal.RanchLevel));
				}
			}
			return best;
		}

		// 등급은 하나씩 오르므로 앞 등급들의 타운홀 중 가장 큰 값
		private static int ToolTownHall(HomesteadCatalog catalog, ToolKind kind, int tier)
		{
			List<ToolTierDef> tiers = catalog.Tool(kind).Tiers;
			if (tier >= tiers.Count)
			{
				return NEVER;
			}
			int townHall = 1;
			for (int i = 0; i <= tier; i++)
			{
				townHall = Math.Max(townHall, tiers[i].TownHall);
			}
			return townHall;
		}

		// 단계 n 은 Levels[n - 1]. 0 단계로 적혀도 지어야 쓰므로 1 단계로 셈. 건물이 없거나 단계가 모자라면 NEVER
		private static int LevelTownHall(HomesteadCatalog catalog, string buildingId, int level)
		{
			int needed = Math.Max(1, level);
			if (catalog.TryGet(buildingId, out BuildingDef building) == false || needed > building.Levels.Count)
			{
				return NEVER;
			}
			int townHall = building.TownHall;
			for (int i = 0; i < needed; i++)
			{
				townHall = Math.Max(townHall, building.Levels[i].TownHall);
			}
			return townHall;
		}

		// 흙 묻은 유물이 나오는 곳이 하나라도 있는가 (갈기, 채집, 낚시, 갱도)
		private static bool HasRelicSource(HomesteadCatalog catalog)
		{
			return catalog.Tuning.TillRelicChance > 0f
				|| catalog.Gathers.Exists(gather => gather.RelicChance > 0f)
				|| catalog.Fishes.Exists(fish => fish.RelicChance > 0f)
				|| catalog.MineBands.Exists(band => band.RelicChance > 0f);
		}

		private static bool IsInAnyBand(HomesteadCatalog catalog, string nodeId)
		{
			foreach (MineBandDef band in catalog.MineBands)
			{
				if (band.Weights.Exists(weight => weight.Node == nodeId && weight.Weight > 0f))
				{
					return true;
				}
			}
			return false;
		}

		private static void CheckCollection(HomesteadCatalog catalog, List<string> problems)
		{
			foreach (WingDef wing in catalog.Wings)
			{
				int pedestals = 0;
				foreach (GoodsDef goods in catalog.Goods)
				{
					if (wing.Kinds.Contains(goods.Kind) == false)
					{
						continue;
					}
					pedestals++;
					if (FirstTownHall(catalog, goods.Id) > catalog.TownHalls.Count)
					{
						problems.Add($"{wing.Id}: 전시품 {goods.Id} 얻을 길이 모두 닫힘");
					}
				}
				int previous = 0;
				foreach (MilestoneDef milestone in wing.Milestones)
				{
					if (milestone.Count <= previous)
					{
						problems.Add($"{wing.Id}: 이정표 {milestone.Count} 가 앞 이정표 {previous} 보다 크지 않음");
					}
					if (milestone.Count > pedestals)
					{
						problems.Add($"{wing.Id}: 이정표 {milestone.Count} 가 받침대 {pedestals} 보다 많음");
					}
					previous = milestone.Count;
				}
			}
		}
	}
}
