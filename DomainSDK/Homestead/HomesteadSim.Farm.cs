using System;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 농사. 갈기, 심기, 물, 거두기. 시들지 않고 물은 빨라지는 이득만
	public sealed partial class HomesteadSim
	{
		public const string FARM = "farm";
		public const string PERK_WATER = "water";

		public int PlotLimit => TownHallDef(State.TownHall).PlotCount + BuildingLevel(FARM) * Tuning.PlotsPerFarmLevel;

		private void EnsurePlots()
		{
			while (State.Plots.Count < PlotLimit)
			{
				State.Plots.Add(new PlotState());
			}
		}

		public PlotPhase PhaseOf(PlotState plot)
		{
			if (plot.Crop.Length == 0)
			{
				return PlotPhase.Empty;
			}
			return plot.Growth >= GrowthNeeded(plot) ? PlotPhase.Ripe : PlotPhase.Growing;
		}

		public float GrowthNeeded(PlotState plot)
		{
			CropDef crop = Catalog.Get<CropDef>(plot.Crop);
			return plot.Harvests > 0 ? crop.RegrowMinutes : crop.GrowMinutes;
		}

		// 0..1. 화면의 자란 정도
		public float Ripeness(PlotState plot) => plot.Crop.Length == 0 ? 0f : Math.Min(1f, plot.Growth / Math.Max(1f, GrowthNeeded(plot)));

		public bool IsWet(PlotState plot) => plot.WetUntil > State.Minutes || HasPerk(PERK_WATER);

		// 마른 칸, 또는 물기가 조금만 남은 칸. 방금 준 칸과 인형이 맡은 밭은 거절
		public bool CanWater(PlotState plot) => plot.Tilled && HasPerk(PERK_WATER) == false && plot.WetUntil - State.Minutes < Tuning.WetRefreshMinutes;

		// 물 준 값으로 남은 게임 분
		public int MinutesLeft(PlotState plot)
		{
			float left = GrowthNeeded(plot) - plot.Growth;
			return left <= 0f ? 0 : (int)Math.Ceiling(IsWet(plot) ? left / Tuning.WetGrowthRate : left);
		}

		private void GrowPlots(long from, long to)
		{
			bool alwaysWet = HasPerk(PERK_WATER);
			foreach (PlotState plot in State.Plots)
			{
				if (plot.Crop.Length == 0)
				{
					continue;
				}
				long wet = alwaysWet ? to - from : Math.Max(0L, Math.Min(to, plot.WetUntil) - from);
				long dry = to - from - wet;
				plot.Growth = Math.Min(GrowthNeeded(plot), plot.Growth + dry + wet * Tuning.WetGrowthRate);
			}
		}

		public HomesteadResult Till(int index)
		{
			PlotState plot = State.Plots[index];
			if (plot.Tilled)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			plot.Tilled = true;
			MaybeFindRelic(Tuning.TillRelicChance);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult Plant(int index, string cropId)
		{
			PlotState plot = State.Plots[index];
			CropDef crop = Catalog.Get<CropDef>(cropId);
			if (plot.Tilled == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			if (plot.Crop.Length > 0)
			{
				return HomesteadResult.Fail(HomesteadFailure.Busy);
			}
			if (Count(crop.Seed) < 1)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, crop.Seed);
			}
			Take(crop.Seed, 1);
			plot.Crop = cropId;
			plot.Growth = 0f;
			plot.Harvests = 0;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult Water(int index)
		{
			PlotState plot = State.Plots[index];
			if (plot.Tilled == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			if (CanWater(plot) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone);
			}
			plot.WetUntil = State.Minutes + Tuning.WetMinutes;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public HomesteadResult Harvest(int index, int season)
		{
			PlotState plot = State.Plots[index];
			if (PhaseOf(plot) != PlotPhase.Ripe)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			CropDef crop = Catalog.Get<CropDef>(plot.Crop);
			int amount = random.Next(crop.YieldMin, crop.YieldMax + 1);
			if (crop.Seasons.Contains(season))
			{
				amount += Tuning.SeasonBonusYield;
			}
			Give(crop.Harvest, WithRest(amount));
			if (crop.RegrowMinutes > 0)
			{
				plot.Harvests++;
				plot.Growth = 0f;
			}
			else
			{
				plot.Crop = string.Empty;
				plot.Harvests = 0;
				plot.Growth = 0f;
			}
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 자라는 작물을 걷어 씨앗으로. 잘못 심은 것을 되돌리는 길
		public HomesteadResult Uproot(int index)
		{
			PlotState plot = State.Plots[index];
			if (plot.Crop.Length == 0)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			Give(Catalog.Get<CropDef>(plot.Crop).Seed, 1);
			plot.Crop = string.Empty;
			plot.Growth = 0f;
			plot.Harvests = 0;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 작물을 한 번도 못 봤어도 씨앗은 파는 곳이 있음. 타운홀 단계가 목록을 넓힘
		public HomesteadResult BuySeed(string cropId, int amount)
		{
			CropDef crop = Catalog.Get<CropDef>(cropId);
			if (State.TownHall < crop.TownHall)
			{
				return HomesteadResult.Fail(HomesteadFailure.TownHallTooLow);
			}
			int price = crop.SeedPrice * amount;
			if (Purse.Nyang < price)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughNyang);
			}
			Purse.Nyang -= price;
			Give(crop.Seed, amount);
			NotifyChanged();
			return HomesteadResult.Success;
		}
	}
}
