using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 빈 받침 상태. 화면이 받침 모습을 고름
	public enum ExhibitState
	{
		Unknown = 0,
		// 본 적은 있는데 지금 없음 (팔았거나 씀)
		Seen = 1,
		Held = 2,
		Donated = 3,
	}

	// 박물관. 기증, 이정표 보상, 유물 손질
	public sealed partial class HomesteadSim
	{
		public const string MUSEUM = "museum";

		public bool IsDonated(string goodsId) => State.Donated.Contains(goodsId);

		public bool CanExhibit(string goodsId) => FindWing(Catalog.Get<GoodsDef>(goodsId)) != null;

		// 이 물건을 받는 관. 받는 관이 없으면 던짐
		public WingDef WingOf(string goodsId)
		{
			WingDef wing = FindWing(Catalog.Get<GoodsDef>(goodsId));
			if (wing == null)
			{
				throw new ArgumentException($"관이 받지 않는 물건 {goodsId}");
			}
			return wing;
		}

		// 갈래로 받거나, 갈래 밖인데 따로 적힌 물건 (목장 산물)
		internal static bool Accepts(WingDef wing, GoodsDef goods) => wing.Kinds.Contains(goods.Kind) || wing.Exhibits.Contains(goods.Id);

		private WingDef FindWing(GoodsDef goods)
		{
			foreach (WingDef wing in Catalog.Wings)
			{
				if (Accepts(wing, goods))
				{
					return wing;
				}
			}
			return null;
		}

		// 관에 걸릴 물건 전부. 정의 순서
		public List<GoodsDef> ExhibitsOf(WingDef wing)
		{
			List<GoodsDef> exhibits = new List<GoodsDef>();
			foreach (GoodsDef goods in Catalog.Goods)
			{
				if (Accepts(wing, goods))
				{
					exhibits.Add(goods);
				}
			}
			return exhibits;
		}

		// 빈 받침이 보일 상태. 놓임이 가장 앞, 그다음 지금 가짐, 본 적 있음, 모름
		public ExhibitState ExhibitStateOf(string goodsId)
		{
			if (IsDonated(goodsId))
			{
				return ExhibitState.Donated;
			}
			if (Count(goodsId) > 0)
			{
				return ExhibitState.Held;
			}
			return IsKnown(goodsId) ? ExhibitState.Seen : ExhibitState.Unknown;
		}

		// 물고기 받침에 놓인 크기. 기록 전에 놓인 것은 0
		public float ExhibitSize(string goodsId) => State.ExhibitSizes.TryGetValue(goodsId, out float size) ? size : 0f;

		// 놓인 것보다 큰 것을 낚은 적이 있음. 크기는 가방에 안 남아서 가장 큰 기록을 가져온다고 봄
		public bool HasLargerFish(string goodsId) =>
			IsDonated(goodsId) && IsFish(goodsId) && State.FishRecords.TryGetValue(FishOf(goodsId), out FishRecord record) && record.BestSize > ExhibitSize(goodsId);

		private bool IsFish(string goodsId) => Catalog.Get<GoodsDef>(goodsId).Kind == GoodsKind.Fish;

		// 물고기 물건의 물고기 id. 물고기가 아니면 던짐
		private string FishOf(string goodsId) => HomesteadRepair.FishOf(Catalog, goodsId);

		public int LargerFishNyang(string goodsId) => (int)Math.Round(Catalog.Get<GoodsDef>(goodsId).Price * Tuning.LargerFishNyangRate);

		public int DonatedCount(WingDef wing)
		{
			int count = 0;
			foreach (GoodsDef goods in ExhibitsOf(wing))
			{
				if (IsDonated(goods.Id))
				{
					count++;
				}
			}
			return count;
		}

		public HomesteadResult Donate(string goodsId)
		{
			if (IsOpen(MUSEUM) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, MUSEUM);
			}
			if (CanExhibit(goodsId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.Unknown, goodsId);
			}
			if (IsDonated(goodsId))
			{
				return SwapLargerFish(goodsId);
			}
			if (Count(goodsId) < 1)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId);
			}
			Take(goodsId, 1);
			if (IsFish(goodsId))
			{
				State.ExhibitSizes[goodsId] = State.FishRecords.TryGetValue(FishOf(goodsId), out FishRecord record) ? record.BestSize : 0f;
			}
			if (IsKnown(goodsId) == false)
			{
				State.Known.Add(goodsId);
			}
			State.Donated.Add(goodsId);
			AddGuildExp(Tuning.DonateGuildExp);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 놓인 물고기보다 큰 것을 가져오면 바꾸고 값의 일부를 냥으로. 작거나 같으면 이미 놓임
		private HomesteadResult SwapLargerFish(string goodsId)
		{
			if (HasLargerFish(goodsId) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone, goodsId);
			}
			if (Count(goodsId) < 1)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId);
			}
			Take(goodsId, 1);
			State.ExhibitSizes[goodsId] = State.FishRecords[FishOf(goodsId)].BestSize;
			Purse.Nyang += LargerFishNyang(goodsId);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		public int ClaimedMilestones(WingDef wing) => State.WingClaims.TryGetValue(wing.Id, out int claimed) ? claimed : 0;

		public bool HasClaimableMilestone(WingDef wing)
		{
			int claimed = ClaimedMilestones(wing);
			return claimed < wing.Milestones.Count && DonatedCount(wing) >= wing.Milestones[claimed].Count;
		}

		// 이정표는 순서대로 하나씩. 받는 순간을 사람이 고름
		public HomesteadResult ClaimMilestone(string wingId, out RewardDef reward)
		{
			WingDef wing = Catalog.Get<WingDef>(wingId);
			reward = null;
			if (HasClaimableMilestone(wing) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			int claimed = ClaimedMilestones(wing);
			reward = wing.Milestones[claimed].Reward;
			State.WingClaims[wingId] = claimed + 1;
			Grant(reward);
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// ---- 유물 손질

		public HomesteadResult BeginRestore()
		{
			if (IsOpen(MUSEUM) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotBuilt, MUSEUM);
			}
			if (State.Restoring != null)
			{
				return HomesteadResult.Fail(HomesteadFailure.Busy);
			}
			if (State.DirtyRelics < 1)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods);
			}
			State.DirtyRelics--;
			State.Restoring = new RelicRestoration { Relic = RollRelic().Id };
			NotifyChanged();
			return HomesteadResult.Success;
		}

		// 손질해 본 유물만 때 이름이 보임. 처음 보는 것은 모양과 색 단서로 골라야 함
		public bool IsGrimeNamed() => State.Restoring != null && IsKnown(Catalog.Get<RelicDef>(State.Restoring.Relic).Goods);

		// 못 본 유물이 더 잘 나옴. 겹침이 길어지지 않게
		private RelicDef RollRelic()
		{
			float total = 0f;
			foreach (RelicDef relic in Catalog.Relics)
			{
				total += RelicWeight(relic);
			}
			float pick = (float)random.NextDouble() * total;
			foreach (RelicDef relic in Catalog.Relics)
			{
				pick -= RelicWeight(relic);
				if (pick < 0f)
				{
					return relic;
				}
			}
			return Catalog.Relics[Catalog.Relics.Count - 1];
		}

		private float RelicWeight(RelicDef relic) => IsKnown(relic.Goods) ? relic.Weight : relic.Weight * Tuning.UnownedRelicBias;

		// 지금 자리의 때에 도구를 댐. 틀려도 망가지지 않고 덤만 줄어듦
		public HomesteadResult Clean(int toolIndex, out bool matched, out bool finished)
		{
			matched = false;
			finished = false;
			if (State.Restoring == null)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotReady);
			}
			RelicDef relic = Catalog.Get<RelicDef>(State.Restoring.Relic);
			matched = relic.Grime[State.Restoring.Cleaned] == toolIndex;
			if (matched == false)
			{
				State.Restoring.Mistakes++;
				NotifyChanged();
				return HomesteadResult.Success;
			}
			State.Restoring.Cleaned++;
			if (State.Restoring.Cleaned >= relic.Grime.Count)
			{
				finished = true;
				if (State.Restoring.Mistakes == 0)
				{
					Purse.Nyang += Tuning.RelicPerfectNyang;
				}
				if (IsDonated(relic.Goods))
				{
					Purse.Nyang += Tuning.DuplicateRelicNyang;
				}
				Give(relic.Goods, 1);
				State.Restoring = null;
			}
			NotifyChanged();
			return HomesteadResult.Success;
		}
	}
}
