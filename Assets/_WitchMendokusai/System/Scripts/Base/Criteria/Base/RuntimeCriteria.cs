using Newtonsoft.Json;

namespace WitchMendokusai
{
	public class RuntimeCriteria : ICriteria, ISavable<RuntimeCriteriaSaveData>
	{
		public Criteria Criteria { get; private set; }

		public CriteriaInfo CriteriaInfo { get; private set; }
		// 한 번만 달성하면 되는지
		public bool JustOnce { get; private set; }
		public bool IsCompleted { get; private set; }

		public bool Evaluate()
		{
			if (JustOnce && IsCompleted)
				return true;

			// Debug.Log(Criteria.Evaluate());
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
			CriteriaInfo = new CriteriaInfo(saveData.CriteriaInfo);
			Criteria = Criteria.CreateCriteria(CriteriaInfo);
			JustOnce = saveData.JustOnce;
			IsCompleted = saveData.IsCompleted;
		}

		public RuntimeCriteriaSaveData Save()
		{
			return new RuntimeCriteriaSaveData
			{
				CriteriaInfo = new CriteriaInfoSaveData(CriteriaInfo),
				JustOnce = JustOnce,
				IsCompleted = IsCompleted
			};
		}

		// 동적인 조건 정보 (i.e. 에디터 타임에 정해지지 않은, 어떤 아이템이 필요하다)
		public RuntimeCriteria(CriteriaInfo criteriaInfo)
		{
			CriteriaInfo = criteriaInfo;
			Criteria = Criteria.CreateCriteria(criteriaInfo);
			JustOnce = criteriaInfo.JustOnce;
		}

		public RuntimeCriteria(RuntimeCriteriaSaveData saveData)
		{
			Load(saveData);
		}
	}
}