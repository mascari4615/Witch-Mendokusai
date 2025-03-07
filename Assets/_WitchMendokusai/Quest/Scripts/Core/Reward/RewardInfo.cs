using System;
using static WitchMendokusai.SOHelper;

namespace WitchMendokusai
{
	[Serializable]
	public struct RewardInfo
	{
		public RewardType Type;
		public DataSO DataSO;
		public int Amount;
	}
}