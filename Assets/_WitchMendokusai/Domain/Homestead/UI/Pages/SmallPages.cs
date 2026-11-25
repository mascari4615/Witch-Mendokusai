using System;
using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 옷장. 짓고 입기
	public sealed class WardrobePage : HomesteadPage
	{
		private static readonly string[] TABS = { "모자", "물들임" };

		public WardrobePage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "옷장";
		protected override string IconKey => HomesteadSim.TAILOR;
		public override IReadOnlyList<string> Tabs => TABS;
		public override bool IsGrid(int tab) => true;
		public override string Status => "겉모습만 바뀝니다";

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			foreach (CostumeDef costume in Catalog.Costumes)
			{
				if ((int)costume.Slot == tab)
					entries.Add(Entry(costume));
			}
			return entries;
		}

		private HomesteadEntry Entry(CostumeDef costume)
		{
			bool owned = Sim.OwnsCostume(costume.Id);
			bool worn = Sim.WornIn(costume.Slot) == costume.Id;
			return new HomesteadEntry
			{
				Id = costume.Id,
				Icon = Art.Icon(costume.Id),
				Name = costume.Name,
				Count = worn ? "입음" : string.Empty,
				IsDim = owned == false,
				IsMarked = owned == false && Sim.CanMakeCostume(costume.Id).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(costume.Id), Title = costume.Name, Note = TABS[(int)costume.Slot] };
					if (owned)
					{
						AddAction(detail, worn ? "벗기" : "입기", HomesteadResult.Success, () => Sim.Wear(costume.Id));
						return detail;
					}
					if (costume.Source == HomesteadSim.REWARD_ONLY)
					{
						detail.Lines.Add("박물관이나 사람에게서 얻습니다.");
						return detail;
					}
					if (Sim.BuildingLevel(HomesteadSim.TAILOR) < costume.Level)
						detail.Lines.Add($"의상실 {costume.Level}단부터");
					AddCost(detail, costume.Cost);
					AddAction(detail, "짓기", Sim.CanMakeCostume(costume.Id), () => Sim.MakeCostume(costume.Id), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}
	}

	// 씨앗 고르기. 빈 밭에서 열리고, 고르면 닫고 심음. 사기도 여기서
	public sealed class SeedPage : HomesteadPage
	{
		private const int BUY_FIVE = 5;
		private static readonly string[] TABS = { "씨앗" };

		private Action<string> onPick = delegate { };

		public SeedPage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "씨앗 주머니";
		protected override string IconKey => HomesteadSim.FARM;
		public override IReadOnlyList<string> Tabs => TABS;
		public override string Status => HomesteadText.Season(Manager.Season);

		public void Ask(Action<string> picked) => onPick = picked;

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			foreach (CropDef crop in Catalog.Crops)
				entries.Add(Entry(crop));
			return entries;
		}

		private HomesteadEntry Entry(CropDef crop)
		{
			int owned = Sim.Count(crop.Seed);
			bool locked = Sim.State.TownHall < crop.TownHall && owned == 0;
			return new HomesteadEntry
			{
				Id = crop.Id,
				Icon = Art.IconOf(Catalog.Get<GoodsDef>(crop.Harvest)),
				Name = crop.Name,
				Sub = locked ? $"회관 {crop.TownHall}단부터" : $"{HomesteadText.Minutes(crop.GrowMinutes)}{(crop.RegrowMinutes > 0 ? ", 다시 열림" : string.Empty)}",
				Count = owned.ToString(),
				IsDim = owned == 0,
				IsMarked = owned > 0 && crop.Seasons.Contains(Manager.Season),
				Detail = () =>
				{
					GoodsDef harvest = Catalog.Get<GoodsDef>(crop.Harvest);
					HomesteadDetail detail = new() { Icon = Art.IconOf(harvest), Title = crop.Name, Note = crop.Seasons.Contains(Manager.Season) ? "제철이라 하나 더 열립니다" : "철이 아니어도 자랍니다" };
					detail.Lines.Add($"자라는 데 {HomesteadText.Minutes(crop.GrowMinutes)}, 물을 주면 더 빨리");
					if (crop.RegrowMinutes > 0)
						detail.Lines.Add($"거둔 뒤 {HomesteadText.Minutes(crop.RegrowMinutes)}마다 다시 열림");
					detail.Lines.Add($"한 번에 {crop.YieldMin}에서 {crop.YieldMax}개, 하나에 {harvest.Price}냥");
					HomesteadResult canPlant = owned > 0 ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, crop.Seed);
					AddAction(detail, "심기", canPlant, () => Done(() => onPick(crop.Id)));
					if (Sim.State.TownHall >= crop.TownHall)
					{
						AddBuy(detail, crop, 1);
						AddBuy(detail, crop, BUY_FIVE);
					}
					return detail;
				},
			};
		}

		private void AddBuy(HomesteadDetail detail, CropDef crop, int amount)
		{
			HomesteadResult can = Sim.Purse.Nyang >= crop.SeedPrice * amount ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.NotEnoughNyang);
			AddAction(detail, $"씨앗 {amount}개 사기  {crop.SeedPrice * amount}냥", can, () => Sim.BuySeed(crop.Id, amount), UiSfxEvent.Purchase);
		}
	}

	// 목장. 데려오기와 지금 있는 동물
	public sealed class RanchPage : HomesteadPage
	{
		private static readonly string[] TABS = { "동물", "데려오기" };

		public RanchPage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "목장";
		protected override string IconKey => HomesteadSim.RANCH;
		public override IReadOnlyList<string> Tabs => TABS;
		public override string Status => $"동물 {Sim.State.Animals.Count} / {Sim.RanchSlots}";
		public override string EmptyText(int tab) => "아직 동물이 없습니다.";

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			if (tab == 0)
			{
				foreach (AnimalState animal in Sim.State.Animals)
					entries.Add(AnimalEntry(animal));
				return entries;
			}
			foreach (AnimalDef animal in Catalog.Animals)
				entries.Add(ShopEntry(animal));
			return entries;
		}

		private HomesteadEntry AnimalEntry(AnimalState animal)
		{
			AnimalDef def = Catalog.Get<AnimalDef>(animal.Id);
			bool ready = Sim.IsProductReady(animal);
			return new HomesteadEntry
			{
				Id = animal.Uid.ToString(),
				Icon = Art.Icon(animal.Id),
				Name = def.Name,
				Sub = ready ? $"{Catalog.Get<GoodsDef>(def.Product).Name} 다 됨" : $"정 {animal.Affinity}",
				IsMarked = ready || animal.CareDay != Sim.Day,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(animal.Id), Title = def.Name, Note = animal.Affinity >= def.FineAffinity ? $"{Catalog.Get<GoodsDef>(def.FineProduct).Name}을 냅니다" : $"{Catalog.Get<GoodsDef>(def.Product).Name}을 냅니다" };
					detail.Bar = animal.Affinity / (float)Catalog.Tuning.AnimalMaxAffinity;
					detail.BarText = $"정 {animal.Affinity}";
					detail.Lines.Add(ready ? "거둘 것이 있습니다." : $"다음까지 {HomesteadText.Minutes(animal.ProductSince + def.IntervalMinutes - Sim.State.Minutes)}");
					HomesteadResult can = ready || animal.CareDay != Sim.Day ? HomesteadResult.Success : HomesteadResult.Fail(HomesteadFailure.DailyLimit);
					AddAction(detail, "쓰다듬기", can, () => Sim.Care(animal.Uid, out bool _), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}

		private HomesteadEntry ShopEntry(AnimalDef animal)
		{
			return new HomesteadEntry
			{
				Id = animal.Id,
				Icon = Art.Icon(animal.Id),
				Name = animal.Name,
				Sub = Sim.BuildingLevel(HomesteadSim.RANCH) < animal.RanchLevel ? $"목장 {animal.RanchLevel}단부터" : Catalog.Get<GoodsDef>(animal.Product).Name,
				IsDim = Sim.BuildingLevel(HomesteadSim.RANCH) < animal.RanchLevel,
				IsMarked = Sim.CanBuyAnimal(animal.Id).Ok,
				Detail = () =>
				{
					HomesteadDetail detail = new() { Icon = Art.Icon(animal.Id), Title = animal.Name, Note = $"{HomesteadText.Minutes(animal.IntervalMinutes)}마다 {Catalog.Get<GoodsDef>(animal.Product).Name}" };
					detail.Lines.Add($"정이 {animal.FineAffinity} 이면 {Catalog.Get<GoodsDef>(animal.FineProduct).Name}");
					AddCost(detail, animal.Cost);
					AddAction(detail, "데려오기", Sim.CanBuyAnimal(animal.Id), () => Sim.BuyAnimal(animal.Id), UiSfxEvent.Purchase);
					return detail;
				},
			};
		}
	}

	// 승강기. 내려갈 깊이 고르기
	public sealed class MinePage : HomesteadPage
	{
		private static readonly string[] TABS = { "깊이" };

		private Action<int> onPick = delegate { };

		public MinePage(HomesteadManager manager) : base(manager)
		{
		}

		public override string Title => "승강기";
		protected override string IconKey => "quarry";
		public override IReadOnlyList<string> Tabs => TABS;
		public override string Status => $"가장 깊이 {Sim.State.MineDeepest} / {Sim.MineMaxDepth}   등불 {Sim.LanternCapacity}";

		public void Ask(Action<int> picked) => onPick = picked;

		public override List<HomesteadEntry> Entries(int tab)
		{
			List<HomesteadEntry> entries = new();
			foreach (int depth in Sim.LiftStops())
			{
				MineBandDef band = Sim.BandAt(depth);
				entries.Add(new HomesteadEntry
				{
					Id = depth.ToString(),
					Icon = Art.Icon("quarry"),
					Name = $"{depth}층",
					Sub = band.Name,
					Detail = () =>
					{
						HomesteadDetail detail = new() { Icon = Art.Icon("quarry"), Title = $"{depth}층", Note = band.Name };
						List<string> names = band.Weights.ConvertAll(weight => NodeText(Catalog.Get<MineNodeDef>(weight.Node)));
						detail.Lines.Add(string.Join(", ", names));
						detail.Lines.Add("바위에 빛이 돌 때 치면 세게 들어갑니다.");
						detail.Lines.Add("등불이 다하면 올라옵니다. 캔 것은 잃지 않습니다.");
						AddAction(detail, "내려가기", HomesteadResult.Success, () => Done(() => onPick(depth)), UiSfxEvent.WindowClose);
						return detail;
					},
				});
			}
			return entries;
		}

		private string NodeText(MineNodeDef node) => node.Tier > Sim.ToolTier(ToolKind.Pickaxe) ? $"{node.Name} (곡괭이가 약함)" : node.Name;
	}
}
