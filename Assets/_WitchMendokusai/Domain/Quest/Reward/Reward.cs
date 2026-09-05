using System;
using System.Collections.Generic;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	// 보상 지급. 꺼낼 곳과 넣을 곳은 부르는 쪽이 줌 (여기서 안 찾음)
	public class Reward
	{
		public static void GetReward(List<RewardInfo> rewards, Inventory itemInventory, GameStat gameStat)
		{
			foreach (RewardInfo reward in rewards)
				GetReward(reward, itemInventory, gameStat);
		}

		public static void GetReward(RewardInfo reward, Inventory itemInventory, GameStat gameStat)
		{
			switch (reward.Type)
			{
				case RewardType.Item:
					ItemData itemData = reward.DataSO as ItemData;
					itemInventory.Add(itemData, reward.Amount);
					break;
				case RewardType.Gold:
					gameStat[GameStatType.NYANG] += reward.Amount;
					break;
				case RewardType.Exp:
					gameStat[GameStatType.VILLAGE_QUEST_EXP] += reward.Amount;
					break;
			}
		}

		public static void GetReward(List<RewardInfoData> rewards, Inventory itemInventory, GameStat gameStat)
		{
			foreach (RewardInfoData reward in rewards)
				GetReward(reward, itemInventory, gameStat);
		}

		public static void GetReward(RewardInfoData reward, Inventory itemInventory, GameStat gameStat)
		{
			switch (reward.Type)
			{
				case RewardType.Item:
					ItemData itemData = GetItemData(reward.DataSOID);
					itemInventory.Add(itemData, reward.Amount);
					break;
				case RewardType.Gold:
					gameStat[GameStatType.NYANG] += reward.Amount;
					break;
				case RewardType.Exp:
					gameStat[GameStatType.VILLAGE_QUEST_EXP] += reward.Amount;
					break;
			}
		}
	}
}