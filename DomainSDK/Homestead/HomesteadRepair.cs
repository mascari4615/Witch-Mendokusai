using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 세이브 경계. 자료에서 사라진 이름을 여기서만 버림. 안쪽 규칙은 모르는 이름에서 그대로 던짐
	public static class HomesteadRepair
	{
		// 자료가 바뀐 뒤 옛 세이브를 맞춤. 이미 받은 이정표의 옷 (보상 자리가 앞으로 옮겨진 것) 과
		// 크기 기록 전에 놓인 물고기 받침 크기. 돌려줌은 새로 준 옷
		public static List<string> CatchUp(HomesteadState state, HomesteadCatalog catalog)
		{
			List<string> granted = new List<string>();
			foreach (WingDef wing in catalog.Wings)
			{
				int claimed = state.WingClaims.TryGetValue(wing.Id, out int count) ? count : 0;
				for (int i = 0; i < claimed && i < wing.Milestones.Count; i++)
				{
					foreach (string costume in wing.Milestones[i].Reward.Costumes)
					{
						if (state.OwnedCostumes.Contains(costume) == false)
						{
							state.OwnedCostumes.Add(costume);
							granted.Add(costume);
						}
					}
				}
			}
			foreach (string goodsId in state.Donated)
			{
				if (catalog.Get<GoodsDef>(goodsId).Kind == GoodsKind.Fish && state.ExhibitSizes.ContainsKey(goodsId) == false)
				{
					state.ExhibitSizes[goodsId] = state.FishRecords.TryGetValue(FishOf(catalog, goodsId), out FishRecord record) ? record.BestSize : 0f;
				}
			}
			return granted;
		}

		// 물고기 물건의 물고기 id. 물고기가 아니거나 짝이 없으면 던짐
		public static string FishOf(HomesteadCatalog catalog, string goodsId)
		{
			foreach (FishDef fish in catalog.Fishes)
			{
				if (fish.Goods == goodsId)
				{
					return fish.Id;
				}
			}
			throw new System.ArgumentException($"물고기가 아닌 물건 {goodsId}");
		}

		public static List<string> DropUnknown(HomesteadState state, HomesteadCatalog catalog)
		{
			List<string> dropped = new List<string>();
			DropKeys<GoodsDef>(state.Store, catalog, dropped);
			DropItems<GoodsDef>(state.Known, catalog, dropped);
			DropItems<GoodsDef>(state.Donated, catalog, dropped);
			DropKeys<BuildingDef>(state.Buildings, catalog, dropped);
			DropKeys<ResidentDef>(state.Residents, catalog, dropped);
			DropKeys<FurnitureDef>(state.OwnedFurniture, catalog, dropped);
			DropItems<CostumeDef>(state.OwnedCostumes, catalog, dropped);
			DropKeys<JobDef>(state.Jobs, catalog, dropped);
			DropKeys<FishDef>(state.FishRecords, catalog, dropped);
			DropKeys<GoodsDef>(state.ExhibitSizes, catalog, dropped);
			Drop<BuildingDef, WorkState>(state.Works, work => work.Building, catalog, dropped);
			Drop<FurnitureDef, PlacedFurniture>(state.Placed, piece => piece.Id, catalog, dropped);
			Drop<PlaceableDef, PlacedPiece>(state.Village, piece => piece.Id, catalog, dropped);
			Drop<AnimalDef, AnimalState>(state.Animals, animal => animal.Id, catalog, dropped);
			Drop<RecipeDef, CraftState>(state.Crafts, craft => craft.Recipe, catalog, dropped);
			Drop<RequestDef, RequestState>(state.Requests, request => request.Template, catalog, dropped);
			Drop<GoodsDef, RequestState>(state.Requests, request => request.Goods, catalog, dropped);
			foreach (PlotState plot in state.Plots)
			{
				if (plot.Crop.Length > 0 && catalog.TryGet<CropDef>(plot.Crop, out CropDef _) == false)
				{
					dropped.Add(plot.Crop);
					plot.Crop = string.Empty;
					plot.Growth = 0f;
					plot.Harvests = 0;
					plot.Stock = 0;
				}
			}
			foreach (CostumeSlot slot in new List<CostumeSlot>(state.Worn.Keys))
			{
				if (catalog.TryGet<CostumeDef>(state.Worn[slot], out CostumeDef _) == false)
				{
					dropped.Add(state.Worn[slot]);
					state.Worn.Remove(slot);
				}
			}
			if (state.Restoring != null && catalog.TryGet<RelicDef>(state.Restoring.Relic, out RelicDef _) == false)
			{
				dropped.Add(state.Restoring.Relic);
				state.Restoring = null;
			}
			return dropped;
		}

		private static void DropKeys<T>(System.Collections.IDictionary map, HomesteadCatalog catalog, List<string> dropped) where T : HomesteadDef
		{
			List<string> gone = new List<string>();
			foreach (string key in map.Keys)
			{
				if (catalog.TryGet<T>(key, out T _) == false)
				{
					gone.Add(key);
				}
			}
			foreach (string key in gone)
			{
				map.Remove(key);
			}
			dropped.AddRange(gone);
		}

		private static void DropItems<T>(List<string> ids, HomesteadCatalog catalog, List<string> dropped) where T : HomesteadDef
		{
			Drop<T, string>(ids, id => id, catalog, dropped);
		}

		private static void Drop<T, TItem>(List<TItem> items, System.Func<TItem, string> idOf, HomesteadCatalog catalog, List<string> dropped) where T : HomesteadDef
		{
			for (int i = items.Count - 1; i >= 0; i--)
			{
				string id = idOf(items[i]);
				if (catalog.TryGet<T>(id, out T _) == false)
				{
					dropped.Add(id);
					items.RemoveAt(i);
				}
			}
		}
	}
}
