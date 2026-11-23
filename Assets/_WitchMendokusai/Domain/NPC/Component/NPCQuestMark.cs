using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// NPC 머리 위 퀘스트 표식 상태. 그림은 NPCNameplateSettings
	/// </summary>
	public enum NPCQuestMark
	{
		None,
		Start,   // 받을 수 있는 퀘스트가 있음 (Locked)
		Waiting, // 진행 중인 퀘스트만 있음
		Reward,  // 완료 보고할 수 있는 퀘스트가 있음
	}

	public static class NPCQuestMarkRule
	{
		/// <summary>
		/// NPC 가 가진 퀘스트 목록에서 표식 하나를 고름. 보상, 시작, 대기 순으로 앞선 것이 이김
		/// </summary>
		public static NPCQuestMark Resolve(IReadOnlyList<QuestSO> quests, QuestManager questManager)
		{
			if (quests == null || quests.Count == 0)
				return NPCQuestMark.None;

			bool hasLocked = false;
			bool hasWorking = false;
			foreach (QuestSO quest in quests)
			{
				RuntimeQuest runtimeQuest = questManager.GetQuest(quest);
				if (runtimeQuest != null && runtimeQuest.State == RuntimeQuestState.CanComplete)
					return NPCQuestMark.Reward;
				if (questManager.GetQuestState(quest.ID) == QuestState.Locked)
					hasLocked = true;
				if (runtimeQuest != null && runtimeQuest.State <= RuntimeQuestState.Working)
					hasWorking = true;
			}

			if (hasLocked)
				return NPCQuestMark.Start;
			if (hasWorking)
				return NPCQuestMark.Waiting;
			return NPCQuestMark.None;
		}
	}
}
