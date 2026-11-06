using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public struct SurvivalEventAnnouncedEvent
	{
		public SurvivalEventType Type;
		public float StartTime;
		public float LeadSeconds;
		public Monster Monster;
		public int Count;
	}

	public struct SurvivalEventStartedEvent
	{
		public SurvivalEventType Type;
		public Monster Monster;
		public int Count;
	}

	public struct EliteKilledEvent
	{
		public Monster Monster;
		public int ExpValue;
	}

	public struct BossSpawnedEvent
	{
		public Monster Monster;
	}

	// 고를 카드가 없어 레벨업이 회복이나 냥으로 바뀐 순간
	public struct LevelUpConvertedEvent
	{
		public int Heal;
		public int Nyang;
	}

	// 판 시작과 리롤, 스킵, 차단 사용 때 잔여 횟수 알림
	public struct CardChoiceChargesChangedEvent
	{
		public int Rerolls;
		public int Skips;
		public int Bans;
	}

	public struct CardBannedEvent
	{
		public CardData Card;
	}

	public struct LevelUpSkippedEvent
	{
		public int RefundExp;
	}

	public struct ChestPickedUpEvent
	{
		public Vector3 Position;
	}

	// Card가 null이면 냥 보상
	public struct ChestReward
	{
		public CardData Card;
		public bool IsEvolution;
		public int Nyang;
	}

	// 보상 지급이 끝난 뒤 발행
	public struct ChestOpenedEvent
	{
		public IReadOnlyList<ChestReward> Rewards;
		public float AutoCloseSeconds;
	}

	public struct ChestClosedEvent
	{
	}
}
