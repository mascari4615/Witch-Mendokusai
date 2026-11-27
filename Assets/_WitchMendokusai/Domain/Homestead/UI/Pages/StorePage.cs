using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 만들고 사고파는 자리. 집 제작대, 마을 공작소, 상점
	public enum StorePlace
	{
		Bench = 0,
		Workshop = 1,
		Shop = 2,
	}

	// 창고, 가공, 가구. 자리마다 보이는 갈래와 나는 물건이 다름
	public sealed class StorePage : HomesteadPage
	{
		private const string TAB_STORE = "창고";
		private const string TAB_CRAFT = "재료";
		private const string TAB_FURNITURE = "가구";
		private const string TAB_HOUSE = "집";
		private const string HOUSE_ID = "house";
		private const int SELL_TEN = 10;
		private const string COLLECT_ID = "collect";

		private static readonly string[] BENCH_TABS = { TAB_CRAFT, TAB_FURNITURE, TAB_STORE, TAB_HOUSE };
		private static readonly string[] WORKSHOP_TABS = { TAB_CRAFT, TAB_FURNITURE };
		private static readonly string[] SHOP_TABS = { TAB_STORE, TAB_FURNITURE };

		private readonly StorePlace place;

		public StorePage(HomesteadManager manager, StorePlace place) : base(manager) => this.place = place;

		public override string Title => place == StorePlace.Bench ? "제작대" : place == StorePlace.Shop ? "상점" : "공작소";
		protected override string IconKey => place == StorePlace.Shop ? HomesteadSim.SHOP : HomesteadSim.WORKSHOP;
		public override IReadOnlyList<string> Tabs => place == StorePlace.Bench ? BENCH_TABS : place == StorePlace.Shop ? SHOP_TABS : WORKSHOP_TABS;
		public override bool IsGrid(int tab) => Tabs[tab] != TAB_CRAFT && Tabs[tab] != TAB_HOUSE;
		public override string Status => place == StorePlace.Workshop ? (Sim.IsBuilt(HomesteadSim.WORKSHOP) ? $"가공 {Sim.BusyCrafts} / {Sim.CraftSlots}" : "공작소를 아직 짓지 않음") : string.Empty;
		public override string EmptyText(int tab) => Tabs[tab] == TAB_STORE ? "창고가 비었습니다." : string.Empty;

		private string Source => place == StorePlace.Bench ? HomesteadSim.BENCH : place == StorePlace.Shop ? HomesteadSim.SHOP : HomesteadSim.WORKSHOP;

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			switch (Tabs[tab])
			{
				case TAB_STORE:
					foreach (GoodsDef goods in Catalog.Goods)
					{
						if (Sim.StoredCount(goods.Id) > 0)
							entries.Add(GoodsEntry(goods));
					}
					break;
				case TAB_CRAFT:
					if (place == StorePlace.Workshop)
						entries.Add(CollectEntry());
					foreach (RecipeDef recipe in Catalog.Recipes)
					{
						if (recipe.Building == Source)
							entries.Add(RecipeEntry(recipe));
					}
					break;
				case TAB_HOUSE:
					entries.Add(HouseEntry());
					break;
				default:
					foreach (FurnitureDef furniture in Catalog.Furniture)
					{
						// 보상으로만 얻는 것은 집 제작대 목록에 같이 보임
						if (furniture.Source == Source || (place == StorePlace.Bench && furniture.Source == HomesteadSim.REWARD_ONLY))
							entries.Add(FurnitureEntry(furniture));
					}
					break;
			}
			return entries;
		}

		// 집 넓히기. 길드, 회관과 상관없음
		private HomesteadEntry HouseEntry()
		{
			return new HomesteadEntry
			{
				Id = HOUSE_ID,
				Icon = Art.Icon(HOUSE_ID),
				Name = "방",
				Sub = Sim.HouseTier.Name,
				IsMarked = Sim.CanExpandHouse().Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(HOUSE_ID), Title = "방", Note = Sim.HouseTier.Name };
					detail.Lines.Add($"방 {Sim.HouseTier.Width} x {Sim.HouseTier.Depth}, 가구 {Sim.State.Placed.Count} / {Sim.HouseTier.PieceLimit}");
					if (Sim.TryGetNextHouseTier(out HouseTierDef next) == false)
						return detail;
					detail.Lines.Add($"다음: {next.Name}. 방 {next.Width} x {next.Depth}, 가구 {next.PieceLimit}");
					AddCost(detail, next.Cost);
					AddAction(detail, "넓히기", Sim.CanExpandHouse(), () => Sim.ExpandHouse(), UiSfxEvent.LevelUp);
					return detail;
				},
			};
		}

		private HomesteadEntry GoodsEntry(GoodsDef goods)
		{
			int stored = Sim.StoredCount(goods.Id);
			return new HomesteadEntry
			{
				Id = goods.Id,
				Icon = Art.IconOf(goods),
				Name = goods.Name,
				Count = stored.ToString(),
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.IconOf(goods), Title = goods.Name, Note = HomesteadText.KindName(goods.Kind) };
					if (goods.Note.Length > 0)
						detail.Lines.Add(goods.Note);
					bool canSell = HomesteadSim.CanSell(goods);
					detail.Lines.Add(canSell ? $"창고 {stored}, 파는 값 {goods.Price}냥" : $"창고 {stored}, 재료는 팔지 않고 의뢰와 제작에");
					if (Sim.CanExhibit(goods.Id))
						detail.Lines.Add(Sim.IsDonated(goods.Id) ? "박물관에 있음" : "박물관에 아직 없음");
					if (canSell)
						AddAction(detail, $"하나 팔기  {goods.Price}냥", HomesteadResult.Success, () => Sim.Sell(goods.Id, 1), UiSfxEvent.Purchase);
					if (canSell && stored >= SELL_TEN)
						AddAction(detail, $"열 개 팔기  {goods.Price * SELL_TEN}냥", HomesteadResult.Success, () => Sim.Sell(goods.Id, SELL_TEN), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}

		private HomesteadEntry CollectEntry()
		{
			int done = Sim.State.Crafts.FindAll(Sim.IsCraftDone).Count;
			return new HomesteadEntry
			{
				Id = COLLECT_ID,
				Icon = Art.Icon(HomesteadSim.WORKSHOP),
				Name = "작업대",
				Sub = Sim.State.Crafts.Count == 0 ? "비어 있음" : $"만드는 중 {Sim.State.Crafts.Count - done}, 다 됨 {done}",
				IsMarked = done > 0,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(HomesteadSim.WORKSHOP), Title = "작업대", Note = "던전에 있는 동안에도 만듭니다." };
					foreach (CraftState craft in Sim.State.Crafts)
					{
						RecipeDef recipe = Catalog.Get<RecipeDef>(craft.Recipe);
						detail.Lines.Add(Sim.IsCraftDone(craft) ? $"{recipe.Name}  다 됨" : $"{recipe.Name}  {HomesteadText.Minutes(craft.ReadyAt - Sim.State.Minutes)}");
					}
					HomesteadResult can = done > 0 ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotReady);
					AddAction(detail, "다 된 것 거두기", can, () => Done(() => Sim.CollectCrafts()), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}

		private HomesteadEntry RecipeEntry(RecipeDef recipe)
		{
			GoodsDef output = Catalog.Get<GoodsDef>(recipe.Output);
			bool isBench = place == StorePlace.Bench;
			bool locked = isBench == false && Sim.BuildingLevel(recipe.Building) < recipe.Level;
			return new HomesteadEntry
			{
				Id = recipe.Id,
				Icon = Art.IconOf(output),
				Name = recipe.Name,
				Sub = locked ? $"공작소 {recipe.Level}단부터" : (isBench ? $"{output.Name} {recipe.Amount}" : $"{output.Name} {recipe.Amount}, {HomesteadText.Minutes(recipe.Minutes)}"),
				Count = Sim.Count(recipe.Output).ToString(),
				IsDim = locked,
				IsMarked = Sim.CanCraft(recipe.Id).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.IconOf(output), Title = recipe.Name, Note = $"{output.Name} {recipe.Amount}개" };
					detail.Lines.Add(isBench ? $"가진 것 {Sim.Count(recipe.Output)}" : $"{HomesteadText.Minutes(recipe.Minutes)} 걸림, 가진 것 {Sim.Count(recipe.Output)}");
					foreach (KeyValuePair<string, int> pair in recipe.Inputs)
						AddGoods(detail, pair.Key, pair.Value);
					AddAction(detail, "만들기", Sim.CanCraft(recipe.Id), () => Sim.StartCraft(recipe.Id));
					int finished = Sim.State.Crafts.FindAll(Sim.IsCraftDone).Count;
					if (isBench == false && finished > 0)
						AddAction(detail, $"다 된 것 {finished}개 거두기", HomesteadResult.Success, () => Done(() => Sim.CollectCrafts()), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}

		private HomesteadEntry FurnitureEntry(FurnitureDef furniture)
		{
			int owned = Sim.OwnedCount(furniture.Id);
			bool rewardOnly = furniture.Source == HomesteadSim.REWARD_ONLY;
			return new HomesteadEntry
			{
				Id = furniture.Id,
				Icon = Art.Icon(furniture.Id),
				Name = furniture.Name,
				Count = owned > 0 ? owned.ToString() : string.Empty,
				IsDim = owned == 0 && (rewardOnly || IsTooEarly(furniture)),
				IsMarked = Sim.CanMakeFurniture(furniture.Id).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(furniture.Id), Title = furniture.Name, Note = $"{furniture.Theme} 결" };
					detail.Lines.Add($"아늑함 {furniture.Comfort}, 크기 {furniture.Width} x {furniture.Depth}");
					detail.Lines.Add($"가진 것 {owned}, 놓은 것 {Sim.PlacedCount(furniture.Id)}");
					if (rewardOnly)
					{
						detail.Lines.Add("박물관이나 사람에게서 얻습니다.");
						return detail;
					}
					if (IsTooEarly(furniture))
						detail.Lines.Add($"공작소 {furniture.Level}단부터");
					AddCost(detail, furniture.Cost);
					AddAction(detail, place == StorePlace.Shop ? "사기" : "만들기", Sim.CanMakeFurniture(furniture.Id), () => Sim.MakeFurniture(furniture.Id), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}

		private bool IsTooEarly(FurnitureDef furniture) => furniture.Source == HomesteadSim.WORKSHOP && Sim.BuildingLevel(HomesteadSim.WORKSHOP) < furniture.Level;
	}
}
