namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch. EventBusBridge = 이벤트 transport (Slice1 ADR), 유지.
	public class UnlockQuestEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			QuestSO quest = effectInfo.Data as QuestSO;
			EventBusBridge.Publish(new QuestUnlockRequestedEvent(quest.ID));
		}
	}
}
