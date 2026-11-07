using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	public enum ShopCartSide
	{
		Buy = 0,
		Sell = 1,
	}

	public enum ShopCartAddResult
	{
		Added = 0,
		CannotAfford = 1,
		BuyLimitReached = 2,
		SellLimitReached = 3,
	}

	// 담는 순간의 물건 조건
	public readonly struct ShopGoods
	{
		public readonly int ItemId;
		public readonly int BuyPrice;
		public readonly int SalePrice;
		public readonly int MaxAmount;
		public readonly int OwnedAmount;

		public ShopGoods(int itemId, int buyPrice, int salePrice, int maxAmount, int ownedAmount)
		{
			ItemId = itemId;
			BuyPrice = buyPrice;
			SalePrice = salePrice;
			MaxAmount = maxAmount;
			OwnedAmount = ownedAmount;
		}
	}

	public sealed class ShopCartLine
	{
		public ShopGoods Goods { get; }
		public ShopCartSide Side { get; }
		public int Amount { get; internal set; }

		internal ShopCartLine(ShopGoods goods, ShopCartSide side)
		{
			Goods = goods;
			Side = side;
		}

		public int UnitPrice => Side == ShopCartSide.Buy ? Goods.BuyPrice : Goods.SalePrice;
		public long Total => (long)UnitPrice * Amount;
		public long SignedTotal => Side == ShopCartSide.Buy ? -Total : Total;
	}

	// 결제 적용 대상 가방
	public interface IShopBag
	{
		// 못 뺀 개수 반환
		int Consume(int itemId, int amount);

		// 못 넣은 개수 반환
		int Add(int itemId, int amount);
	}

	public readonly struct ShopCheckoutResult
	{
		public readonly bool Succeeded;
		public readonly long Wallet;
		public readonly long Net;
		public readonly int SoldAmount;
		public readonly int BoughtAmount;
		public readonly int UnplacedAmount;

		public ShopCheckoutResult(bool succeeded, long wallet, long net, int soldAmount, int boughtAmount, int unplacedAmount)
		{
			Succeeded = succeeded;
			Wallet = wallet;
			Net = net;
			SoldAmount = soldAmount;
			BoughtAmount = boughtAmount;
			UnplacedAmount = unplacedAmount;
		}
	}

	// 상점 장바구니 판정의 정본. 엔진 무관
	public sealed class ShopCart
	{
		private readonly List<ShopCartLine> lines = new();
		private readonly int lineLimit;

		public ShopCart(int lineLimit)
		{
			if (lineLimit <= 0)
				throw new ArgumentOutOfRangeException(nameof(lineLimit));
			this.lineLimit = lineLimit;
		}

		public IReadOnlyList<ShopCartLine> Lines => lines;
		public bool IsEmpty => lines.Count == 0;

		public long Net
		{
			get
			{
				long net = 0;
				foreach (ShopCartLine line in lines)
					net += line.SignedTotal;
				return net;
			}
		}

		public long WalletAfter(long wallet) => wallet + Net;
		public long Shortfall(long wallet) => Math.Max(0, -WalletAfter(wallet));
		public bool CanCheckout(long wallet) => IsEmpty == false && WalletAfter(wallet) >= 0;

		public ShopCartLine Find(int itemId, ShopCartSide side)
		{
			foreach (ShopCartLine line in lines)
				if (line.Goods.ItemId == itemId && line.Side == side)
					return line;
			return null;
		}

		public int AmountOf(int itemId, ShopCartSide side) => Find(itemId, side)?.Amount ?? 0;

		// 장비처럼 1개짜리는 보유와 무관하게 1개, 셀 수 있는 물건은 최대 소지까지
		public int LimitOf(ShopGoods goods, ShopCartSide side)
		{
			if (side == ShopCartSide.Sell)
				return Math.Max(0, goods.OwnedAmount);
			if (goods.MaxAmount == 1)
				return 1;
			return Math.Min(lineLimit, Math.Max(0, goods.MaxAmount - goods.OwnedAmount));
		}

		public ShopCartAddResult AddOne(ShopGoods goods, ShopCartSide side, long wallet)
		{
			if (side == ShopCartSide.Buy && goods.BuyPrice > wallet)
				return ShopCartAddResult.CannotAfford;

			ShopCartLine line = Find(goods.ItemId, side);
			int amount = line?.Amount ?? 0;
			if (amount >= LimitOf(goods, side))
				return side == ShopCartSide.Sell ? ShopCartAddResult.SellLimitReached : ShopCartAddResult.BuyLimitReached;

			if (line == null)
			{
				line = new ShopCartLine(goods, side);
				lines.Add(line);
			}
			line.Amount++;
			return ShopCartAddResult.Added;
		}

		public bool RemoveOne(int itemId, ShopCartSide side)
		{
			ShopCartLine line = Find(itemId, side);
			if (line == null)
				return false;
			line.Amount--;
			if (line.Amount <= 0)
				lines.Remove(line);
			return true;
		}

		public void Clear() => lines.Clear();

		// 판매 먼저 적용, 실제 빠진 개수만 입금. 구매는 지갑과 가방에 들어간 개수만 차감
		public ShopCheckoutResult Checkout(long wallet, IShopBag bag)
		{
			if (CanCheckout(wallet) == false)
				return new ShopCheckoutResult(false, wallet, Net, 0, 0, 0);

			long current = wallet;
			int sold = 0;
			int bought = 0;
			int unplaced = 0;

			foreach (ShopCartLine line in lines)
			{
				if (line.Side != ShopCartSide.Sell)
					continue;
				int amount = line.Amount - bag.Consume(line.Goods.ItemId, line.Amount);
				sold += amount;
				current += (long)line.UnitPrice * amount;
			}

			foreach (ShopCartLine line in lines)
			{
				if (line.Side != ShopCartSide.Buy)
					continue;
				int affordable = line.UnitPrice > 0 ? (int)Math.Min(line.Amount, current / line.UnitPrice) : line.Amount;
				int amount = affordable - bag.Add(line.Goods.ItemId, affordable);
				bought += amount;
				unplaced += line.Amount - amount;
				current -= (long)line.UnitPrice * amount;
			}

			long net = current - wallet;
			lines.Clear();
			return new ShopCheckoutResult(true, current, net, sold, bought, unplaced);
		}
	}
}
