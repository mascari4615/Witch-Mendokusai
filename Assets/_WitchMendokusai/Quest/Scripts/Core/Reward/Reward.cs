using System;
using System.Collections.Generic;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	public class Reward
	{
		public static void GetReward(List<RewardInfo> rewards)
		{
			foreach (RewardInfo reward in rewards)
				GetReward(reward);
		}

		public static void GetReward(RewardInfo reward)
		{
			switch (reward.Type)
			{
				case RewardType.Item:
					ItemData itemData = reward.DataSO as ItemData;
					SOManager.Instance.ItemInventory.Add(itemData, reward.Amount);
					break;
				case RewardType.Gold:
					SOManager.Instance.Nyang.RuntimeValue += reward.Amount;
					break;
				case RewardType.Exp:
					SOManager.Instance.VQExp.RuntimeValue += reward.Amount;
					break;
			}
		}

		public static void GetReward(List<RewardInfoData> rewards)
		{
			foreach (RewardInfoData reward in rewards)
				GetReward(reward);
		}

		public static void GetReward(RewardInfoData reward)
		{
			switch (reward.Type)
			{
				case RewardType.Item:
					ItemData itemData = GetItemData(reward.DataSOID);
					SOManager.Instance.ItemInventory.Add(itemData, reward.Amount);
					break;
				case RewardType.Gold:
					SOManager.Instance.Nyang.RuntimeValue += reward.Amount;
					break;
				case RewardType.Exp:
					SOManager.Instance.VQExp.RuntimeValue += reward.Amount;
					break;
			}
		}
	}
}