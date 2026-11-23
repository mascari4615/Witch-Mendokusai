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

	// 몬스터 한 마리 처치 (정예와 보스 포함). 처치 연쇄 표시가 셈
	public struct MonsterKilledEvent
	{
		public Vector3 Position;
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

	public struct BossKilledEvent
	{
		public Monster Monster;
	}

	// 보스 웨이브 시작 전 예고. 웨이브마다 한 번
	public struct BossAnnouncedEvent
	{
		public Monster Monster;
		public float StartTime;
		public float LeadSeconds;
	}

	// 한 판의 난이도 단계가 올라간 순간
	public struct DifficultyRaisedEvent
	{
		public DungeonDifficulty Difficulty;
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
