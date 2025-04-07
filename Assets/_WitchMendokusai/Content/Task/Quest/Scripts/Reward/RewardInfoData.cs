using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct RewardInfoData
	{
		public RewardType Type;
		public int DataSOID;
		public int Amount;

		public RewardInfoData(RewardInfo rewardInfo)
		{
			Type = rewardInfo.Type;
			DataSOID = rewardInfo.DataSO ? rewardInfo.DataSO.ID : DataSO.NONE_ID;
			Amount = rewardInfo.Amount;
		}
	}
}