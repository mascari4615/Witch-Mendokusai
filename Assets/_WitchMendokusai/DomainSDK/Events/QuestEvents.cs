using System;

namespace WitchMendokusai
{
	public record QuestAddedEvent(RuntimeQuest Quest);

	public record QuestCompletedEvent(Guid? Guid);
}
