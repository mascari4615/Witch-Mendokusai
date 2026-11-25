using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 들에서 하는 일. 채집, 낚시 걸린 것 고르기와 기록, 갱도 내려가기
	public sealed partial class HomesteadSim
	{
		// ---- 채집. 자리 id 는 배치가 정함 (마을 하나에 같은 종류가 여럿)

		public bool IsGatherReady(string spotId) => State.GatherReady.TryGetValue(spotId, out long ready) == false || State.Minutes >= ready;

		public bool CanGather(GatherDef gather) => ToolTier(gather.Tool) >= gather.Tier;

		// 몇 번 쳐야 하는지. 도구가 세면 줄고, 모자라면 못 침
		public int GatherHits(GatherDef gather) => Math.Max(1, (int)Math.Ceiling(gather.Hits / (float)ToolPower(gather.Tool)));

		public HomesteadResult Gather(string spotId, string gatherId)
		{
			GatherDef gather = Catalog.Get<GatherDef>(gatherId);
			if (IsGatherReady(spotId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			if (CanGather(gather) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.ToolTooWeak, gather.Tool.ToString());
			}
			for (int i = 0; i < gather.Rolls; i++)
			{
				DropDef drop = Roll(gather.Drops);
				Give(drop.Goods, WithRest(RollAmount(drop)));
			}
			MaybeFindRelic(gather.RelicChance);
			State.GatherReady[spotId] = State.Minutes + gather.RespawnMinutes;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// ---- 낚시

		public bool CanBite(FishDef fish, string water, int hour, int season)
		{
			if (fish.Waters.Contains(water) == false || ToolTier(ToolKind.Rod) < fish.RodTier)
			{
				return false;
			}
			if (fish.Seasons.Count > 0 && fish.Seasons.Contains(season) == false)
			{
				return false;
			}
			// 밤을 넘기는 시간대는 From 이 To 보다 큼
			return fish.HourFrom <= fish.HourTo
				? hour >= fish.HourFrom && hour < fish.HourTo
				: hour >= fish.HourFrom || hour < fish.HourTo;
		}

		public FishDef PickFish(string water, int hour, int season)
		{
			float total = 0f;
			List<FishDef> open = new List<FishDef>();
			foreach (FishDef fish in Catalog.Fishes)
			{
				if (CanBite(fish, water, hour, season))
				{
					open.Add(fish);
					total += fish.Weight;
				}
			}
			if (open.Count == 0)
			{
				throw new InvalidOperationException($"물 {water} 에 {hour}시, 계절 {season} 에 무는 것이 없음");
			}
			float pick = (float)random.NextDouble() * total;
			foreach (FishDef fish in open)
			{
				pick -= fish.Weight;
				if (pick < 0f)
				{
					return fish;
				}
			}
			return open[open.Count - 1];
		}

		public FishingDuel StartDuel(FishDef fish) => new FishingDuel(fish, Tuning, ToolTier(ToolKind.Rod), random);

		// 낚음 확정. 크기는 무작위, 완벽하면 큰 쪽으로. 반환은 크기와 신기록 여부
		public float Land(FishDef fish, bool perfect, out bool isRecord)
		{
			double roll = random.NextDouble();
			if (perfect)
			{
				roll = Math.Max(roll, random.NextDouble());
			}
			float size = (float)Math.Round(fish.SizeMin + (fish.SizeMax - fish.SizeMin) * roll, 1);
			if (State.FishRecords.TryGetValue(fish.Id, out FishRecord record) == false)
			{
				record = new FishRecord();
				State.FishRecords.Add(fish.Id, record);
			}
			record.Count++;
			isRecord = size > record.BestSize;
			if (isRecord)
			{
				record.BestSize = size;
			}
			Give(fish.Goods, 1);
			MaybeFindRelic(fish.RelicChance);
			NotifyChanged();
			return size;
		}

		// ---- 갱도

		public MineBandDef BandAt(int depth)
		{
			MineBandDef last = null;
			foreach (MineBandDef band in Catalog.MineBands)
			{
				if (depth >= band.From)
				{
					last = band;
				}
			}
			return last ?? throw new InvalidOperationException($"깊이 {depth} 구간 없음");
		}

		public int MineMaxDepth
		{
			get
			{
				int max = 0;
				foreach (MineBandDef band in Catalog.MineBands)
				{
					max = Math.Max(max, band.To);
				}
				return max;
			}
		}

		// 승강기가 서는 깊이. 닿아 본 곳까지만
		public List<int> LiftStops()
		{
			List<int> stops = new List<int> { 1 };
			for (int depth = Tuning.LiftStep; depth <= State.MineDeepest; depth += Tuning.LiftStep)
			{
				stops.Add(depth);
			}
			return stops;
		}

		public int LanternCapacity => Tuning.LanternSwings + ToolTier(ToolKind.Pickaxe) * Tuning.LanternPerPickTier;

		public MineRun EnterMine(int depth)
		{
			if (LiftStops().Contains(depth) == false)
			{
				throw new ArgumentException($"승강기가 안 서는 깊이 {depth}");
			}
			MineRun run = new MineRun(this, LanternCapacity);
			run.Enter(depth);
			return run;
		}

		internal void ReachDepth(int depth)
		{
			if (depth > State.MineDeepest)
			{
				State.MineDeepest = depth;
				NotifyChanged();
			}
		}

		internal MineNodeDef RollMineNode(MineBandDef band)
		{
			float total = 0f;
			foreach (MineWeightDef weight in band.Weights)
			{
				total += weight.Weight;
			}
			float pick = (float)random.NextDouble() * total;
			foreach (MineWeightDef weight in band.Weights)
			{
				pick -= weight.Weight;
				if (pick < 0f)
				{
					return Catalog.Get<MineNodeDef>(weight.Node);
				}
			}
			return Catalog.Get<MineNodeDef>(band.Weights[band.Weights.Count - 1].Node);
		}

		internal void BreakMineNode(MineNodeDef node, MineBandDef band)
		{
			DropDef drop = Roll(node.Drops);
			Give(drop.Goods, WithRest(RollAmount(drop)));
			MaybeFindRelic(band.RelicChance);
			NotifyChanged();
		}

		internal int RandomIndex(int count) => random.Next(count);
	}
}
