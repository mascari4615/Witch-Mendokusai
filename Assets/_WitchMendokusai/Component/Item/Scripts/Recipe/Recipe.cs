using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	[Serializable]
	public struct ItemInfo
	{
		public ItemData ItemData;
		public int Amount;
	}

	[Serializable]
	public struct Recipe
	{
		public RecipeType Type;
		public int Amount;
		public List<ItemInfo> Items;
		public int PriceNyang;
		public float Percentage;

		public List<RewardInfo> FailureRewards;
		public List<RewardInfo> SuccessRewards;
	}
}