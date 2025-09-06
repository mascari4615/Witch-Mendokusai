using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum QuestGroup
	{
		None = -1,
		Normal = 0,
		VillageRequest = 1,
		Achievement = 2,
		Dungeon = 100,
	}

	public enum QuestType
	{
		Main,
		Side
	}

	[Flags]
	public enum QuestTag
	{
		Hidden = 1 << 0,
	}

	public enum QuestState
	{
		Locked,
		Unlocked,
		Completed
	}

	[Serializable]
	public struct QuestInfo
	{
		public QuestGroup Group;
		[EnumButtons] public QuestType Type;
		public QuestTag Tag;
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