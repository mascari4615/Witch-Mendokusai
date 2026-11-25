using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 공작소. 창고 보기와 팔기, 가공, 가구 짓기
	public sealed class StorePage : HomesteadPage
	{
		public const int TAB_STORE = 0;
		public const int TAB_CRAFT = 1;
		public const int TAB_FURNITURE = 2;
		private const int SELL_TEN = 10;
		private const string COLLECT_ID = "collect";

		private static readonly string[] TABS = { "창고", "가공", "가구" };

		public StorePage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "공작소";
		protected override string IconKey => HomesteadSim.WORKSHOP;
		public override IReadOnlyList<string> Tabs => TABS;
		public override bool IsGrid(int tab) => tab != TAB_CRAFT;
		public override string Status => Sim.IsBuilt(HomesteadSim.WORKSHOP) ? $"가공 {Sim.State.Crafts.Count} / {Sim.CraftSlots}" : "공작소를 아직 짓지 않음";
		public override string EmptyText(int tab) => tab == TAB_STORE ? "창고가 비었습니다." : string.Empty;

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			switch (tab)
			{
				case TAB_STORE:
					foreach (GoodsDef goods in Catalog.Goods)
					{
						if (Sim.StoredCount(goods.Id) > 0)
							entries.Add(GoodsEntry(goods));
					}
					break;
				case TAB_CRAFT:
					entries.Add(CollectEntry());
					foreach (RecipeDef recipe in Catalog.Recipes)
						entries.Add(RecipeEntry(recipe));
					break;
				default:
					foreach (FurnitureDef furniture in Catalog.Furniture)
						entries.Add(FurnitureEntry(furniture));
					break;
			}
			return entries;
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
			bool locked = Sim.BuildingLevel(recipe.Building) < recipe.Level;
			return new HomesteadEntry
			{
				Id = recipe.Id,
				Icon = Art.IconOf(output),
				Name = recipe.Name,
				Sub = locked ? $"공작소 {recipe.Level}단부터" : $"{output.Name} {recipe.Amount}, {HomesteadText.Minutes(recipe.Minutes)}",
				Count = Sim.Count(recipe.Output).ToString(),
				IsDim = locked,
				IsMarked = Sim.CanCraft(recipe.Id).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.IconOf(output), Title = recipe.Name, Note = $"{output.Name} {recipe.Amount}개" };
					detail.Lines.Add($"{HomesteadText.Minutes(recipe.Minutes)} 걸림, 가진 것 {Sim.Count(recipe.Output)}");
					foreach (KeyValuePair<string, int> pair in recipe.Inputs)
						AddGoods(detail, pair.Key, pair.Value);
					AddAction(detail, "만들기", Sim.CanCraft(recipe.Id), () => Sim.StartCraft(recipe.Id));
					int finished = Sim.State.Crafts.FindAll(Sim.IsCraftDone).Count;
					if (finished > 0)
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
				IsDim = owned == 0 && (rewardOnly || Sim.BuildingLevel(HomesteadSim.WORKSHOP) < furniture.Level),
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
					if (Sim.BuildingLevel(HomesteadSim.WORKSHOP) < furniture.Level)
						detail.Lines.Add($"공작소 {furniture.Level}단부터");
					AddCost(detail, furniture.Cost);
					AddAction(detail, "짓기", Sim.CanMakeFurniture(furniture.Id), () => Sim.MakeFurniture(furniture.Id), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}
	}
}
