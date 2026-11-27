using System;
using System.Collections.Generic;

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
			return RipeLoads(plot) > 0 ? PlotPhase.Ripe : PlotPhase.Growing;
		}

		// 한 번 거두면 받는 열매 수. 재수확 작물은 쌓인 수에 지금 다 익은 하나를 더함 (옛 세이브는 쌓인 수 없이 다 익은 채)
		public int RipeLoads(PlotState plot)
		{
			if (plot.Crop.Length == 0)
			{
				return 0;
			}
			int ripeNow = plot.Growth >= GrowthNeeded(plot) ? 1 : 0;
			return IsRegrow(plot) ? Math.Min(Tuning.RegrowStockCap, plot.Stock + ripeNow) : ripeNow;
		}

		public float GrowthNeeded(PlotState plot)
		{
			CropDef crop = Catalog.Get<CropDef>(plot.Crop);
			return plot.Harvests > 0 || plot.Stock > 0 ? crop.RegrowMinutes : crop.GrowMinutes;
		}

		private bool IsRegrow(PlotState plot) => Catalog.Get<CropDef>(plot.Crop).RegrowMinutes > 0;

		// 0..1. 화면의 자란 정도
		public float Ripeness(PlotState plot)
		{
			if (plot.Crop.Length == 0)
			{
				return 0f;
			}
			return RipeLoads(plot) > 0 ? 1f : Math.Min(1f, plot.Growth / Math.Max(1f, GrowthNeeded(plot)));
		}

		public bool IsWet(PlotState plot) => plot.WetUntil > State.Minutes || HasPerk(PERK_WATER);

		// 마른 칸, 또는 물기가 조금만 남은 칸. 방금 준 칸과 인형이 맡은 밭은 거절
		public bool CanWater(PlotState plot) => plot.Tilled && HasPerk(PERK_WATER) == false && plot.WetUntil - State.Minutes < Tuning.WetRefreshMinutes;

		// 물 준 값으로 남은 게임 분
		public int MinutesLeft(PlotState plot)
		{
			if (RipeLoads(plot) > 0)
			{
				return 0;
			}
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
				float grown = plot.Growth + dry + wet * Tuning.WetGrowthRate;
				if (IsRegrow(plot))
				{
					BankRegrow(plot, grown);
				}
				else
				{
					plot.Growth = Math.Min(GrowthNeeded(plot), grown);
				}
			}
		}

		// 재수확 작물은 다 익은 열매를 쌓고 이어서 자람. 상한에 닿으면 멈춤 (시들지 않음)
		private void BankRegrow(PlotState plot, float grown)
		{
			while (plot.Stock < Tuning.RegrowStockCap && grown >= GrowthNeeded(plot))
			{
				grown -= GrowthNeeded(plot);
				plot.Stock++;
			}
			plot.Growth = plot.Stock >= Tuning.RegrowStockCap ? 0f : grown;
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
			plot.Stock = 0;
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
			int loads = RipeLoads(plot);
			int amount = 0;
			for (int i = 0; i < loads; i++)
			{
				amount += random.Next(crop.YieldMin, crop.YieldMax + 1);
				if (crop.Seasons.Contains(season))
				{
					amount += Tuning.SeasonBonusYield;
				}
			}
			Give(crop.Harvest, WithRest(amount));
			if (Chance(Tuning.HarvestSeedChance))
			{
				Give(crop.Seed, 1);
			}
			if (crop.RegrowMinutes > 0)
			{
				// 이어 자라던 몫은 남김. 다 익은 채 쌓인 것만 거둠
				if (plot.Growth >= GrowthNeeded(plot))
				{
					plot.Growth = 0f;
				}
				plot.Harvests++;
				plot.Stock = 0;
			}
			else
			{
				plot.Crop = string.Empty;
				plot.Harvests = 0;
				plot.Growth = 0f;
				plot.Stock = 0;
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
			plot.Stock = 0;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 던전에서 돌아올 때 가끔 씨앗 하나. 한 단계 위 타운홀 씨앗까지 나와 다음 단계 작물을 먼저 심을 수 있음
		private void MaybeBringExpeditionSeed()
		{
			if (Chance(Tuning.ExpeditionSeedChance) == false)
			{
				return;
			}
			int townHall = State.TownHall + Tuning.ExpeditionSeedTownHallAhead;
			List<CropDef> open = Catalog.Crops.FindAll(crop => crop.TownHall <= townHall);
			if (open.Count > 0)
			{
				Give(open[random.Next(open.Count)].Seed, 1);
			}
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
