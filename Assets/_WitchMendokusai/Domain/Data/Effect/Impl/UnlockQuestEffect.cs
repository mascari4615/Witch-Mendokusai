using UnityEngine;

namespace WitchMendokusai
{
	public class UnlockQuestEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			QuestManagerBridge.UnlockQuest(effectInfo.Data as QuestSO);
		}
	}
}