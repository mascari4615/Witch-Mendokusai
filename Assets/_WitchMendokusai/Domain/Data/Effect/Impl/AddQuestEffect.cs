using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public class AddQuestEffect : IEffect
	{
		public void Apply(EffectInfo effectInfo)
		{
			QuestSO quest = effectInfo.Data as QuestSO;
			QuestManager.Instance.AddQuest(new(quest));
		}
	}
}