namespace WitchMendokusai
{
	public class UnlockQuestEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			QuestSO quest = effectInfo.Data as QuestSO;
			EventBusBridge.Publish(new QuestUnlockRequestedEvent(quest.ID));
		}
	}
}
