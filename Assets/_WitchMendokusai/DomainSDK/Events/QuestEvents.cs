using System;

namespace WitchMendokusai
{
	public record QuestAddedEvent(RuntimeQuest Quest) : IEvent;

	public record QuestCompletedEvent(Guid? Guid) : IEvent;

	public record QuestDetailRequestedEvent(QuestSO Quest) : IEvent;
}
