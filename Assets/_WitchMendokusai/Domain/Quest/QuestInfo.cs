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

		/// <summary>끝낼 때 가운데 팝업을 띄울지 (마도서 장 해금, 레시피 같은 진행 노드). 업적 알림은 업적 시스템이 따로 띄움</summary>
		public bool ShowCompletePopup;
	}
}