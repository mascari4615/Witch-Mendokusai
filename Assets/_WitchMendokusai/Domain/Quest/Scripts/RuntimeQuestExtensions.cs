namespace WitchMendokusai
{
	public static class RuntimeQuestExtensions
	{
		public static float GetProgress(this RuntimeQuest quest)
		{
			if (quest.State == RuntimeQuestState.Working)
			{
				if (DataManager.Instance.WorkManager.TryGetWorkByQuestGuid(quest.Guid, out Work work))
					return work.GetProgress();
				return 0f;
			}

			if (quest.Criteria.Count == 0)
				return 1f;

			float progress = 0f;
			foreach (RuntimeCriteria runtimeCriteria in quest.Criteria)
				progress += runtimeCriteria.GetProgress();
			return progress / quest.Criteria.Count;
		}

		public static string GetProgressText(this RuntimeQuest quest)
		{
			if (quest.State == RuntimeQuestState.Working)
			{
				if (DataManager.Instance.WorkManager.TryGetWorkByQuestGuid(quest.Guid, out Work work))
					return work.GetProgress().ToString("P0");
				return string.Empty;
			}

			if (quest.Criteria.Count == 0)
				return "100%";

			float curValue = 0f;
			float targetValue = 0f;
			foreach (RuntimeCriteria runtimeCriteria in quest.Criteria)
			{
				curValue += runtimeCriteria.GetCurValue();
				targetValue += runtimeCriteria.GetTargetValue();
			}
			return $"{curValue} / {targetValue}";
		}
	}
}
