using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임 업적 장부. QuestManager 의 퀘스트 상태 중 완료된 Achievement 타입 QuestSO ID 를 매번 새로 셈.
	/// 캐싱 없음 (세이브 로드로 상태 사전이 통째로 바뀌어도 그대로 맞음)
	/// </summary>
	public sealed class QuestAchievementLedger : IAchievementLedger
	{
		private readonly QuestManager questManager;

		public QuestAchievementLedger(QuestManager questManager)
		{
			this.questManager = questManager;
		}

		public IReadOnlyCollection<int> CompletedAchievementIds
		{
			get
			{
				List<int> completed = new();
				foreach (KeyValuePair<int, QuestState> pair in questManager.GetQuestStates())
				{
					if (pair.Value != QuestState.Completed)
					{
						continue;
					}
					if (SOHelper.GetQuestSO(pair.Key).Data.Type == QuestType.Achievement)
					{
						completed.Add(pair.Key);
					}
				}
				return completed;
			}
		}
	}
}
