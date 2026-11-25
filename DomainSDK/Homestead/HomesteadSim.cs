using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 마을 밖 주머니. 던전에서 가져온 물건과 냥
	public interface IHomesteadPurse
	{
		int Nyang { get; set; }
		int CountItem(int itemId);
		// 못 뺀 개수 반환
		int ConsumeItem(int itemId, int amount);
	}

	// 생활 규칙의 한 자리. 갈래마다 partial 파일 하나
	// - 시간은 밖에서 AdvanceMinutes 로 밀어 줌. 자라기와 만들기는 지나간 분으로 계산해 던전에 있는 동안에도 흐름
	// - 벌 없음: 시들기, 부서지기, 떠나기 없음. 안 한 일은 그대로 기다림
	public sealed partial class HomesteadSim
	{
		public HomesteadCatalog Catalog { get; }
		public HomesteadState State { get; }
		public IHomesteadPurse Purse { get; }

		private readonly Random random;

		public event Action<HomesteadGain> Gained = delegate { };
		public event Action Changed = delegate { };

		private HomesteadTuning Tuning => Catalog.Tuning;

		public HomesteadSim(HomesteadCatalog catalog, HomesteadState state, IHomesteadPurse purse, Random random)
		{
			Catalog = catalog;
			State = state;
			Purse = purse;
			this.random = random;
			EnsurePlots();
		}

		public long Day => State.Minutes / Tuning.MinutesPerDay;

		// 지나간 게임 분. 되감긴 시계는 0 취급
		public void AdvanceMinutes(long minutes)
		{
			if (minutes <= 0)
			{
				return;
			}
			long from = State.Minutes;
			State.Minutes += minutes;
			GrowPlots(from, State.Minutes);
			Changed();
		}

		private void NotifyChanged() => Changed();

		// ---- 창고

		// 창고와 던전 가방을 합친 수
		public int Count(string goodsId)
		{
			int stored = State.Store.TryGetValue(goodsId, out int amount) ? amount : 0;
			GoodsDef goods = Catalog.Get<GoodsDef>(goodsId);
			return goods.ItemId >= 0 ? stored + Purse.CountItem(goods.ItemId) : stored;
		}

		public int StoredCount(string goodsId) => State.Store.TryGetValue(goodsId, out int amount) ? amount : 0;

		public bool IsKnown(string goodsId) => State.Known.Contains(goodsId);

		public void Give(string goodsId, int amount)
		{
			if (amount <= 0)
			{
				return;
			}
			Catalog.Get<GoodsDef>(goodsId);
			State.Store[goodsId] = StoredCount(goodsId) + amount;
			bool isNew = IsKnown(goodsId) == false;
			if (isNew)
			{
				State.Known.Add(goodsId);
			}
			Gained(new HomesteadGain(goodsId, amount, isNew));
		}

		// 창고부터 쓰고 모자라면 던전 가방
		private void Take(string goodsId, int amount)
		{
			int fromStore = Math.Min(amount, StoredCount(goodsId));
			if (fromStore > 0)
			{
				State.Store[goodsId] -= fromStore;
				if (State.Store[goodsId] == 0)
				{
					State.Store.Remove(goodsId);
				}
			}
			int rest = amount - fromStore;
			if (rest == 0)
			{
				return;
			}
			int missing = Purse.ConsumeItem(Catalog.Get<GoodsDef>(goodsId).ItemId, rest);
			if (missing > 0)
			{
				throw new InvalidOperationException($"{goodsId} {missing}개 모자람. 값 판정 뒤에만 부를 것");
			}
		}

		public HomesteadResult CanPay(CostDef cost)
		{
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				if (Count(pair.Key) < pair.Value)
				{
					return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, pair.Key);
				}
			}
			return Purse.Nyang < cost.Nyang ? HomesteadResult.Fail(HomesteadFailure.NotEnoughNyang) : HomesteadResult.Success;
		}

		private void Pay(CostDef cost)
		{
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				Take(pair.Key, pair.Value);
			}
			if (cost.Nyang > 0)
			{
				Purse.Nyang -= cost.Nyang;
			}
		}

		public HomesteadResult Sell(string goodsId, int amount)
		{
			GoodsDef goods = Catalog.Get<GoodsDef>(goodsId);
			if (amount <= 0 || StoredCount(goodsId) < amount)
			{
				return HomesteadResult.Fail(HomesteadFailure.NotEnoughGoods, goodsId);
			}
			if (goods.Price <= 0)
			{
				return HomesteadResult.Fail(HomesteadFailure.Locked, goodsId);
			}
			Take(goodsId, amount);
			Purse.Nyang += goods.Price * amount;
			NotifyChanged();
			return HomesteadResult.Success;
		}

		private void Grant(RewardDef reward)
		{
			if (reward.Nyang > 0)
			{
				Purse.Nyang += reward.Nyang;
			}
			foreach (KeyValuePair<string, int> pair in reward.Goods)
			{
				Give(pair.Key, pair.Value);
			}
			foreach (string furniture in reward.Furniture)
			{
				AddFurniture(furniture, 1);
			}
			foreach (string costume in reward.Costumes)
			{
				AddCostume(costume);
			}
			AddGuildExp(reward.GuildExp);
		}

		// ---- 뽑기

		private DropDef Roll(List<DropDef> drops)
		{
			float total = 0f;
			foreach (DropDef drop in drops)
			{
				total += drop.Weight;
			}
			float pick = (float)random.NextDouble() * total;
			foreach (DropDef drop in drops)
			{
				pick -= drop.Weight;
				if (pick < 0f)
				{
					return drop;
				}
			}
			return drops[drops.Count - 1];
		}

		private int RollAmount(DropDef drop) => random.Next(drop.Min, drop.Max + 1);

		private bool Chance(float chance) => chance > 0f && random.NextDouble() < chance;

		// 집에서 쉰 날은 손으로 거둔 것이 가끔 두 배
		private int WithRest(int amount) => IsRested && Chance(State.RestedRank * Tuning.RestDoubleChancePerRank) ? amount * 2 : amount;

		private void MaybeFindRelic(float chance)
		{
			if (Chance(chance))
			{
				State.DirtyRelics++;
				RelicFound();
			}
		}

		public event Action RelicFound = delegate { };

		// ---- 도구

		public int ToolTier(ToolKind kind) => State.Tools.TryGetValue(kind, out int tier) ? tier : 0;

		public int ToolPower(ToolKind kind) => Catalog.Tool(kind).Tiers[ToolTier(kind)].Power;

		public bool TryGetNextToolTier(ToolKind kind, out ToolTierDef next)
		{
			List<ToolTierDef> tiers = Catalog.Tool(kind).Tiers;
			int index = ToolTier(kind) + 1;
			next = index < tiers.Count ? tiers[index] : null;
			return next != null;
		}

		public HomesteadResult CanUpgradeTool(ToolKind kind)
		{
			if (TryGetNextToolTier(kind, out ToolTierDef next) == false)
			{
				return HomesteadResult.Fail(HomesteadFailure.AtMaxLevel);
			}
			return State.TownHall < next.TownHall ? HomesteadResult.Fail(HomesteadFailure.TownHallTooLow) : CanPay(next.Cost);
		}

		public HomesteadResult UpgradeTool(ToolKind kind)
		{
			HomesteadResult result = CanUpgradeTool(kind);
			if (result.Ok == false)
			{
				return result;
			}
			TryGetNextToolTier(kind, out ToolTierDef next);
			Pay(next.Cost);
			State.Tools[kind] = ToolTier(kind) + 1;
			NotifyChanged();
			return HomesteadResult.Success;
		}
	}
}
