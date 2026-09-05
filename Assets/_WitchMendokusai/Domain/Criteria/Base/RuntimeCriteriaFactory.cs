namespace WitchMendokusai
{
	public static class RuntimeCriteriaFactory
	{
		public static RuntimeCriteria FromCriteriaInfo(CriteriaInfo criteriaInfo, CriteriaContext context = null)
		{
			RuntimeCriteriaSaveData saveData = new()
			{
				CriteriaInfo = criteriaInfo.ToSaveData(),
				JustOnce = criteriaInfo.JustOnce,
				IsCompleted = false,
			};
			Criteria criteria = CriteriaFactory.Create(criteriaInfo, context);
			return new RuntimeCriteria(saveData, criteria);
		}

		public static RuntimeCriteria FromSaveData(RuntimeCriteriaSaveData saveData, CriteriaContext context = null)
		{
			CriteriaInfo criteriaInfo = new(saveData.CriteriaInfo);
			Criteria criteria = CriteriaFactory.Create(criteriaInfo, context);
			return new RuntimeCriteria(saveData, criteria);
		}
	}
}
