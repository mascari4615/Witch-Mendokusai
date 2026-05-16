using System;

namespace WitchMendokusai
{
	public record QuestAddedEvent(RuntimeQuest Quest);

	public record QuestCompletedEvent(Guid? Guid, int QuestSOID, QuestType Type);

	public record QuestWorkStartedEvent(Guid? QuestGuid, int WorkerID, float WorkTime);

	public record QuestDetailRequestedEvent(int QuestSOID);

	// POCO Effect → 매니저 명령 의도 (TASK-WM-107). Effect 는 QuestManager 를 모름 — 이벤트만 발행.
	// DomainSDK ⊥ Domain SO 타입 → QuestSO 대신 int QuestSOID (QuestDetailRequestedEvent 컨벤션 정합).
	public record QuestAddRequestedEvent(int QuestSOID);

	public record QuestUnlockRequestedEvent(int QuestSOID);
}
