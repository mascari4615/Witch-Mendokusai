namespace WitchMendokusai
{
	public static class RewardInfoExtensions
	{
		public static RewardInfoData ToInfoData(this RewardInfo rewardInfo)
		{
			return new RewardInfoData
			{
				Type = rewardInfo.Type,
				DataSOID = rewardInfo.DataSO ? rewardInfo.DataSO.ID : DataSO.NONE_ID,
				Amount = rewardInfo.Amount,
			};
		}
	}
}
