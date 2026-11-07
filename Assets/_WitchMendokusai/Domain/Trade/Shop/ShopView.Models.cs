using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public sealed class ShopTabModel
	{
		public string Name;
		public int Count;
		public bool IsSell;
	}

	public enum ShopPriceKind
	{
		Buy = 0,
		BuyUnaffordable = 1,
		Free = 2,
		Sell = 3,
	}

	public sealed class ShopCellModel
	{
		public Sprite Icon;
		public int Owned;
		public ShopPriceKind PriceKind;
		public int Price;
		public int CartAmount;
		public bool IsSellSide;
		public bool IsDimmed;
	}

	public enum ShopCompareTone
	{
		Same = 0,
		Better = 1,
		Worse = 2,
	}

	public sealed class ShopCompareRow
	{
		public SurvivalUIKind Kind;
		public string KindName;
		public int Selected;
		public int Best;
		public string BestOwner;
		public string Difference;
		public ShopCompareTone Tone;
	}

	public sealed class ShopInfoModel
	{
		public Sprite Icon;
		public string Name;
		public string Description;
		public bool HasDescription;
		public bool IsWorn;
		public string CategoryName;
		public int Owned;
		public int MaxAmount;
		public int SalePrice;
		public List<ShopCompareRow> CompareRows;
	}

	public sealed class ShopCartRowModel
	{
		public Sprite Icon;
		public string Name;
		public bool IsSell;
		public int Amount;
		public long Total;
	}

	public sealed class ShopCartModel
	{
		public List<ShopCartRowModel> Rows = new();
		public long Wallet;
		public long Net;
		public long WalletAfter;
		public bool CanPay;
	}
}
