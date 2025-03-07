using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	[Serializable]
	public struct Recipe
	{
		public RecipeType Type;
		public int amount;
		public List<ItemInfo> Items;
		public int priceNyang;
		public float Percentage;

		public List<RewardInfo> FailureRewards;
		public List<RewardInfo> SuccessRewards;
	}
}