namespace WitchMendokusai
{
	public static class RuntimeCriteriaFactory
	{
		public static RuntimeCriteria FromCriteriaInfo(CriteriaInfo criteriaInfo)
		{
			RuntimeCriteriaSaveData saveData = new()
			{
				CriteriaInfo = criteriaInfo.ToSaveData(),
				JustOnce = criteriaInfo.JustOnce,
				IsCompleted = false,
			};
			Criteria criteria = CriteriaFactory.Create(criteriaInfo);
			return new RuntimeCriteria(saveData, criteria);
		}

		public static RuntimeCriteria FromSaveData(RuntimeCriteriaSaveData saveData)
		{
			CriteriaInfo criteriaInfo = new(saveData.CriteriaInfo);
			Criteria criteria = CriteriaFactory.Create(criteriaInfo);
			return new RuntimeCriteria(saveData, criteria);
		}
	}
}
