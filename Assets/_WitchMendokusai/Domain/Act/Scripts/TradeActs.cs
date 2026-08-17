using WitchMendokusai.DomainSDK.Act;

namespace WitchMendokusai
{
	// 거둔 것의 두 갈래길 중 「파는 쪽」을 행동으로 적는다 (TASK-WM-410, 기획 확정 2026-08-17).
	//
	// ★ 왜 행동인가: 팔기도 대가가 있는 일이다(물건이 나가고 돈이 들어온다). 원장에 얹으면
	//   「물건이 없으면 못 판다」·「전부 되거나 전부 안 된다」가 공짜로 따라온다.
	// ★ 시간은 부르는 쪽이 정한다 — 상점 앞에서 흥정하는 데 몇 분이 드는지는 이 코드가 정할 일이 아니다.
	public static class TradeActs
	{
		/// <summary>
		/// 이 물건 <paramref name="count"/> 개를 판다. 값은 아이템이 들고 있는 판매가(SalePrice) 그대로 —
		/// 같은 수를 두 곳에 안 적는다(수치노출 룰).
		/// </summary>
		public static ActSpec Sell(ItemData item, int count, int minutes = 0)
		{
			if (item == null || count <= 0)
			{
				return ActSpec.Free;
			}

			int price = item.SalePrice * count;

			return new ActSpec(
				minutes,
				null,
				new[]
				{
					new ActResourceDelta(new ResourceId(item.ID), -count),
					new ActResourceDelta(WalletActPool.NYANG, price),
				});
		}

		/// <summary>이 물건 <paramref name="count"/> 개를 산다. 값은 아이템의 구매가(PurchasePrice).</summary>
		public static ActSpec Buy(ItemData item, int count, int minutes = 0)
		{
			if (item == null || count <= 0)
			{
				return ActSpec.Free;
			}

			int price = item.PurchasePrice * count;

			return new ActSpec(
				minutes,
				null,
				new[]
				{
					new ActResourceDelta(WalletActPool.NYANG, -price),
					new ActResourceDelta(new ResourceId(item.ID), count),
				});
		}
	}
}
