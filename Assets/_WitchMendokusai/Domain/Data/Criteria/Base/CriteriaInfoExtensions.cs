namespace WitchMendokusai
{
	public static class CriteriaInfoExtensions
	{
		public static CriteriaInfoSaveData ToSaveData(this CriteriaInfo criteriaInfo)
		{
			return new CriteriaInfoSaveData
			{
				Type = criteriaInfo.Type,
				DataID = criteriaInfo.Data.ID,
				ComparisonOperator = criteriaInfo.ComparisonOperator,
				Value = criteriaInfo.Value,
				JustOnce = criteriaInfo.JustOnce,
			};
		}
	}
}
