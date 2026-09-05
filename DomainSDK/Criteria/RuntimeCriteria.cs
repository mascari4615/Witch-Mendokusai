namespace WitchMendokusai
{
	public class RuntimeCriteria : ICriteria, ISavable<RuntimeCriteriaSaveData>
	{
		public Criteria Criteria { get; private set; }

		// 한 번만 달성하면 되는지
		public bool JustOnce { get; private set; }
		public bool IsCompleted { get; private set; }

		private CriteriaInfoSaveData criteriaInfoSaveData;

		public RuntimeCriteria(RuntimeCriteriaSaveData saveData, Criteria criteria)
		{
			Load(saveData);
			Criteria = criteria;
		}

		public bool Evaluate()
		{
			if (JustOnce && IsCompleted)
			{
				return true;
			}

			return IsCompleted = Criteria.Evaluate();
		}

		public int GetCurValue()
		{
			return Criteria.GetCurValue();
		}

		public int GetTargetValue()
		{
			return Criteria.GetTargetValue();
		}

		public float GetProgress()
		{
			return Criteria.GetProgress();
		}

		public void Load(RuntimeCriteriaSaveData saveData)
		{
			criteriaInfoSaveData = saveData.CriteriaInfo;
			JustOnce = saveData.JustOnce;
			IsCompleted = saveData.IsCompleted;
		}

		public RuntimeCriteriaSaveData Save()
		{
			return new RuntimeCriteriaSaveData
			{
				CriteriaInfo = criteriaInfoSaveData,
				JustOnce = JustOnce,
				IsCompleted = IsCompleted,
			};
		}
	}
}
