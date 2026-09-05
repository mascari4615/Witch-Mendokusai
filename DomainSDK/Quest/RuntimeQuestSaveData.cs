using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	// Json.NET 전용 저장 DTO — Unity 직렬화 대상이 아니라 [Serializable] 을 달지 않는다(GameData 주석 참조).
	public struct RuntimeQuestSaveData
	{
		public Guid? Guid;
		public RuntimeQuestState State;

		public int SO_ID;

		public string Name;
		public string Description;

		public QuestType Type;
		public List<GameEventType> GameEvents;
		public List<RuntimeCriteriaSaveData> Criteria;
		public List<EffectInfoData> CompleteEffects;
		public List<EffectInfoData> RewardEffects;
		public List<RewardInfoData> Rewards;

		public float WorkTime;
		public bool AutoWork;
		public bool AutoComplete;
	}
}