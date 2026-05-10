using System;

namespace WitchMendokusai
{
	public record QuestAddedEvent(RuntimeQuest Quest) : IEvent;

	public record QuestCompletedEvent(Guid? Guid, int QuestSOID, QuestType Type) : IEvent;

	public record QuestWorkStartedEvent(Guid? QuestGuid, int WorkerID, float WorkTime) : IEvent;

	public record QuestDetailRequestedEvent(int QuestSOID) : IEvent;
}
