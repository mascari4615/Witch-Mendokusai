namespace WitchMendokusai
{
	// TASK-WM-107 Slice 3-4b — 단일 ctx dispatch. EventBusBridge = 이벤트 transport (Slice1 ADR — 상태
	// Bridge 아님, 매니저 비의존), 유지. ctx 불요 (이벤트만 발행).
	public class AddQuestEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo, EffectContext context)
		{
			QuestSO quest = effectInfo.Data as QuestSO;
			EventBusBridge.Publish(new QuestAddRequestedEvent(quest.ID));
		}
	}
}
