using System;

namespace WitchMendokusai
{
	public record QuestAddedEvent(RuntimeQuest Quest);

	public record QuestCompletedEvent(Guid? Guid, int QuestSOID, QuestType Type);

	public record QuestWorkStartedEvent(Guid? QuestGuid, int WorkerID, float WorkTime);

	public record QuestDetailRequestedEvent(int QuestSOID);
}
