using System;

namespace WitchMendokusai
{
	public interface IQuestManager
	{
		void AddQuest(RuntimeQuest quest);
		RuntimeQuest GetQuest(Guid? guid);
		void CompleteQuest(Guid? guid);
		int GetQuestCount(QuestType questType);
	}
}
