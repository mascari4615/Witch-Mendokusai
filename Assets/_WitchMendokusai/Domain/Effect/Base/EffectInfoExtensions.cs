namespace WitchMendokusai
{
	public static class EffectInfoExtensions
	{
		public static EffectInfoData ToInfoData(this EffectInfo effectInfo)
		{
			return new EffectInfoData
			{
				Type = effectInfo.Type,
				DataSoID = effectInfo.Data ? effectInfo.Data.ID : DataSO.NONE_ID,
				ArithmeticOperator = effectInfo.ArithmeticOperator,
				Value = effectInfo.Value,
			};
		}
	}
}
