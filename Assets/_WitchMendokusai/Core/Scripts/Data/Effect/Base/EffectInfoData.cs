using System;

namespace WitchMendokusai
{
	[Serializable]
	public struct EffectInfoData
	{
		public EffectType Type;
		public int DataSoID;
		public ArithmeticOperator ArithmeticOperator;
		public int Value;

		public EffectInfoData(EffectInfo rewardInfo)
		{
			Type = rewardInfo.Type;
			DataSoID = rewardInfo.Data ? rewardInfo.Data.ID : DataSO.NONE_ID;
			ArithmeticOperator = rewardInfo.ArithmeticOperator;
			Value = rewardInfo.Value;
		}
	}
}