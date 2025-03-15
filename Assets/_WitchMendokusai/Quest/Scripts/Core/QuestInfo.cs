using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	[Serializable]
	public struct QuestInfo
	{
		public QuestType Type;
		public List<EffectInfo> UnlockEffects;
		public List<GameEventType> GameEvents;
		public List<CriteriaInfo> Criteria;
		public List<EffectInfo> CompleteEffects;
		public List<EffectInfo> RewardEffects;
		public List<RewardInfo> Rewards;

		public float WorkTime;
		public bool AutoWork;
		public bool AutoComplete;
	}
}