using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 박물관. 기증, 이정표 보상, 유물 손질
	public sealed partial class HomesteadSim
	{
		public const string MUSEUM = "museum";

		public bool IsDonated(string goodsId) => State.Donated.Contains(goodsId);

		public bool CanExhibit(string goodsId)
		{
			GoodsKind kind = Catalog.Get<GoodsDef>(goodsId).Kind;
			foreach (WingDef wing in Catalog.Wings)
			{
				if (wing.Kinds.Contains(kind))
				{
					return true;
				}
			}
			return false;
		}

		// 관에 걸릴 물건 전부. 정의 순서
		public List<GoodsDef> ExhibitsOf(WingDef wing)
		{
			List<GoodsDef> exhibits = new List<GoodsDef>();
			foreach (GoodsDef goods in Catalog.Goods)
			{
				if (wing.Kinds.Contains(goods.Kind))
				{
					exhibits.Add(goods);
				}
			}
			return exhibits;
		}

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
				return HomesteadResult.Fail(HomesteadFailure.AlreadyDone, goodsId);
			}
			if (Count(goodsId) < 1)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId);
			}
			Take(goodsId, 1);
			if (IsKnown(goodsId) == false)
			{
				State.Known.Add(goodsId);
			}
			State.Donated.Add(goodsId);
			AddGuildExp(Tuning.DonateGuildExp);
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
