namespace WitchMendokusai
{
	public class AddQuestEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			QuestSO quest = effectInfo.Data as QuestSO;
			EventBusBridge.Publish(new QuestAddRequestedEvent(quest.ID));
		}
	}
}
