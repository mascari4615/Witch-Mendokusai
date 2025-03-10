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
					DataManager.Instance.GameStat[GameStatType.NYANG] += reward.Amount;
					break;
				case RewardType.Exp:
					DataManager.Instance.GameStat[GameStatType.VILLAGE_QUEST_EXP] += reward.Amount;
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
					DataManager.Instance.GameStat[GameStatType.NYANG] += reward.Amount;
					break;
				case RewardType.Exp:
					DataManager.Instance.GameStat[GameStatType.VILLAGE_QUEST_EXP] += reward.Amount;
					break;
			}
		}
	}
}