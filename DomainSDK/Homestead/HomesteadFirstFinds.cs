using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 큰 알림 한 장의 내용
	public readonly struct FirstFindCard
	{
		public readonly string Title;
		public readonly string Text;
		public readonly IReadOnlyList<GoodsDef> Goods;

		public FirstFindCard(string title, string text, IReadOnlyList<GoodsDef> goods)
		{
			Title = title;
			Text = text;
			Goods = goods;
		}
	}

	// 처음 얻은 전시품을 모아 큰 알림 한 장으로. 같은 순간 여럿이면 한 장에 묶음
	// 관에 안 걸리는 물건 (씨앗, 재료, 가공품) 은 얻은 것 줄만
	public sealed class HomesteadFirstFinds
	{
		private readonly HomesteadSim sim;
		private readonly List<GoodsDef> pending = new List<GoodsDef>();

		public HomesteadFirstFinds(HomesteadSim sim)
		{
			this.sim = sim;
		}

		public bool HasPending => pending.Count > 0;

		public static WingDef WingOf(HomesteadSim sim, string goodsId)
		{
			GoodsKind kind = sim.Catalog.Get<GoodsDef>(goodsId).Kind;
			foreach (WingDef wing in sim.Catalog.Wings)
			{
				if (wing.Kinds.Contains(kind))
				{
					return wing;
				}
			}
			return null;
		}

		public static int KnownCount(HomesteadSim sim, WingDef wing)
		{
			int count = 0;
			foreach (GoodsDef goods in sim.ExhibitsOf(wing))
			{
				if (sim.IsKnown(goods.Id))
				{
					count++;
				}
			}
			return count;
		}

		// 카드에 실을 것이면 true
		public bool Add(HomesteadGain gain)
		{
			if (gain.IsNew == false || WingOf(sim, gain.GoodsId) == null)
			{
				return false;
			}
			GoodsDef goods = sim.Catalog.Get<GoodsDef>(gain.GoodsId);
			if (pending.Contains(goods))
			{
				return false;
			}
			pending.Add(goods);
			return true;
		}

		// 모인 것을 카드 한 장으로 꺼내고 비움
		public FirstFindCard Take()
		{
			List<GoodsDef> goods = new List<GoodsDef>(pending);
			pending.Clear();
			if (goods.Count == 1)
			{
				WingDef wing = WingOf(sim, goods[0].Id);
				return new FirstFindCard(goods[0].Name, $"처음 얻음  {wing.Name} {KnownCount(sim, wing)} / {sim.ExhibitsOf(wing).Count}", goods);
			}
			List<string> names = new List<string>();
			foreach (GoodsDef each in goods)
			{
				names.Add(each.Name);
			}
			return new FirstFindCard($"처음 얻은 것 {goods.Count}", string.Join(", ", names), goods);
		}
	}
}
