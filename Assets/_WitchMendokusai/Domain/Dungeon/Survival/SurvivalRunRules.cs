using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public class ChestRewardCountWeight
	{
		[Min(1)] public int Count = 1;
		[Min(0f)] public float Weight = 1f;
		[Min(0f), Tooltip("행운 1 (영구 행운 100%) 당 더하는 가중치. 0이면 행운 무관")] public float LuckWeight = 0f;

		public float WeightWith(float luck) => Weight + LuckWeight * luck;
	}

	// 한 판의 리롤, 스킵, 차단, 상자, 진화 규칙
	[CreateAssetMenu(fileName = nameof(SurvivalRunRules), menuName = "WM/Variable/" + nameof(SurvivalRunRules))]
	public class SurvivalRunRules : ScriptableObject
	{
		[Header("Card choice charges")]
		[Min(0), Tooltip("판 시작 리롤 횟수")] public int RerollCharges = 3;
		[Min(0), Tooltip("판 시작 스킵 횟수")] public int SkipCharges = 2;
		[Min(0), Tooltip("판 시작 차단 횟수")] public int BanCharges = 2;
		[Range(0f, 1f), Tooltip("스킵 환급 경험치, 현재 필요 경험치 대비 비율")] public float SkipExpRefundRatio = 0.25f;

		[Header("Exp curve (provisional values)")]
		[Min(1), Tooltip("임시값. 레벨 0에서 1까지 필요 경험치")] public int ExpBaseRequired = 30;
		[Tooltip("임시값. FromLevel 오름차순. Increment는 레벨당 증가량, BarrierExtra는 FromLevel 한 레벨에만 더하는 장벽")]
		public List<ExpCurveBand> ExpCurveBands = new();
		[Min(1f), Tooltip("임시값. 장벽 레벨에서 획득 경험치 배율")] public float ExpBarrierGainMultiplier = 2f;

		[Header("Chest")]
		[Tooltip("비어 있으면 처치 자리에서 바로 개봉")] public ChestObject ChestPrefab;
		[Range(0f, 1f), Tooltip("엘리트 처치 상자 확률")] public float EliteChestChance = 1f;
		public List<ChestRewardCountWeight> ChestRewardCounts = new()
		{
			new ChestRewardCountWeight { Count = 1, Weight = 70f },
			new ChestRewardCountWeight { Count = 3, Weight = 25f },
			new ChestRewardCountWeight { Count = 5, Weight = 5f },
		};
		[Min(0), Tooltip("줄 카드가 없을 때 보상 한 칸당 냥")] public int ChestFallbackNyang = 20;
		[Min(0f), Tooltip("개봉 뒤 자동으로 닫는 실제 시간 초. 0이면 CloseChest 호출까지 유지")] public float ChestAutoCloseSeconds = 1.5f;

		// 상자 보상 개수. 가중치에 행운 항 (ChestRewardCountWeight.LuckWeight) 포함, 기본 0이라 행운 무관
		public int PickChestRewardCount(float luck, float roll01)
		{
			float total = 0f;
			foreach (ChestRewardCountWeight entry in ChestRewardCounts)
				total += entry.WeightWith(luck);
			float roll = roll01 * total;
			foreach (ChestRewardCountWeight entry in ChestRewardCounts)
			{
				roll -= entry.WeightWith(luck);
				if (roll <= 0f)
					return entry.Count;
			}
			return ChestRewardCounts[^1].Count;
		}

		[Header("Random mode")]
		[Tooltip("판 시작 무기. 자동 공격이 없으면 경험치를 못 얻음")] public EquipmentData RandomModeStartEquipment;
		[Min(1), Tooltip("장비 칸 수. 시작 무기 포함, 인형 고유 장비 제외")] public int RandomModeSlotCount = 6;
		[Min(0), Tooltip("레벨업 한 번에 제시하는 새 장비 수")] public int RandomModeNewOfferCount = 1;

		[Header("Deck convergence")]
		[Tooltip("덱 제시 가중치. n 번째 값은 이번 판에 그 덱 카드를 n장 받았을 때. 표보다 많이 받으면 마지막 값. 비우거나 전부 같으면 균등. 0 이하는 빼지 않고 0.001 로 드물게. 기본 {1, 4} 는 한 장이라도 받은 덱이 네 배 자주 나옴")]
		public List<float> DeckWeightByPicks = new() { 1f, 4f };

		[Header("Evolution")]
		[Tooltip("덱에 넣지 않는 카드. 조건은 CardData.Requirements, 지급은 상자 첫 보상")] public List<CardData> EvolutionCards = new();
		[Tooltip("트리 시작 카드를 차단하려 할 때 문구")] public string BanRootDeniedText = "시작 카드는 차단할 수 없어요.";

		[Header("Difficulty")]
		[Min(1f), Tooltip("난이도 단계 (쉬움, 보통, 어려움) 가 오르는 간격 초. HUD 의 남은 시간 고리도 이 값")] public float DifficultyStepSeconds = 180f;

		[Header("Enemy growth over time (2026-11-16 balance)")]
		[Min(0f), Tooltip("분당 적 최대 체력 증가 비율. 0.35 면 10분에 4.5배. 생성 시점 경과 분 기준")] public float EnemyHpGrowthPerMinute = 0.35f;
		[Min(0f), Tooltip("적 피해가 1씩 오르는 간격 분. 0이면 성장 없음")] public float EnemyDamageStepMinutes = 3f;
		[Min(0), Tooltip("간격마다 더하는 적 피해")] public int EnemyDamagePerStep = 1;

		public double EnemyHpMultiplier(double elapsedMinutes) => 1d + EnemyHpGrowthPerMinute * elapsedMinutes;
		public int EnemyDamageBonus(double elapsedMinutes) => EnemyDamageStepMinutes <= 0f ? 0 : (int)(elapsedMinutes / EnemyDamageStepMinutes) * EnemyDamagePerStep;

		[Header("Wave density (provisional values)")]
		[Min(0), Tooltip("임시값. 동시 적 수가 이 값 이상이면 주기 생성과 보충 중지. 0이면 상한 없음")] public int EnemySoftCap = 300;
		[Min(0), Tooltip("임시값. 동시 적 수가 이 값 이상이면 무리 사건도 중지. 보스와 정예는 예외. 0이면 상한 없음")] public int EnemyHardCap = 500;
		[Min(0), Tooltip("임시값. 최소 적 수 보충 한 번에 만들 최대 마릿수. 0이면 부족분 전부")] public int RefillBurstMax = 30;

		[Header("Offscreen enemies (mc36, provisional values)")]
		[Min(0f), Tooltip("화면 밖 일반 적 중 플레이어와 이 거리 미만은 가까운 단계")] public float OffscreenMidDistance = 20f;
		[Min(0f), Tooltip("화면 밖 일반 적 중 플레이어와 이 거리 이상은 먼 단계")] public float OffscreenFarDistance = 35f;
		[Min(0f), Tooltip("화면 밖 가까운 단계 이동 갱신 간격 초. 0이면 매 물리 틱")] public float OffscreenNearInterval = 0.1f;
		[Min(0f), Tooltip("화면 밖 중간 단계 이동 갱신 간격 초")] public float OffscreenMidInterval = 0.2f;
		[Min(0f), Tooltip("화면 밖 먼 단계 이동 갱신 간격 초")] public float OffscreenFarInterval = 0.4f;
		[Min(0f), Tooltip("화면 밖에 이 초 넘게 있던 일반 적은 드롭 없이 풀 반납. 보스와 정예 제외. 0이면 끔")] public float OffscreenRetireSeconds = 30f;

		public float OffscreenUpdateInterval(float distance)
		{
			if (distance < OffscreenMidDistance)
				return OffscreenNearInterval;
			return distance < OffscreenFarDistance ? OffscreenMidInterval : OffscreenFarInterval;
		}

		[Header("Floor items (provisional values)")]
		[Tooltip("처치 바닥 아이템에 적 정지와 화면 청소를 남김. 회복과 자석은 부서지는 물건 전용 (mc10)")] public bool KillDropFreezeAndClear = true;
		[Min(0f), Tooltip("적 처치마다 적 정지 아이템이 떨어질 확률, 퍼센트. 행운 배율 적용 (2026-11-18 0.25 에서 낮춤)")] public float FreezeDropPercent = 0.08f;
		[Min(0f), Tooltip("적 처치마다 화면 청소 아이템이 떨어질 확률, 퍼센트. 행운 배율 적용 (2026-11-18 0.15 에서 낮춤)")] public float ClearDropPercent = 0.05f;
		[Min(0f), Tooltip("임시값. 적 정지 아이템이 화면 안 몬스터에 거는 얼음 시간 초. 보스 비율은 UnitStatusConfig 얼음 줄")] public float FreezeSeconds = 8f;

		[Header("Card rarity (weight per card)")]
		[Min(0f), Tooltip("일반 카드 한 장의 고르기 가중치")] public float CommonCardWeight = 1f;
		[Min(0f), Tooltip("희귀 카드 한 장의 고르기 가중치. 행운 배율 적용")] public float RareCardWeight = 0.5f;
		[Min(0f), Tooltip("영웅 카드 한 장의 고르기 가중치. 행운 배율 적용")] public float EpicCardWeight = 0.2f;

		// 행운은 영구 강화 하나 (UpgradeData.Luck, 0~1). 닿는 곳마다 계수 하나
		[Header("Luck (mc18, one luck value)")]
		[Min(0f), Tooltip("카드 희귀도. 희귀, 영웅 가중치 x (1 + 운 x 이 값). 운은 영구 행운 + 장비 레벨 x EquipmentLevelLuck")] public float RarityLuckScale = 1f;
		[Min(0f), Tooltip("카드 희귀도 추첨에서 그 장비 레벨 하나당 운에 더하는 값 (mc16)")] public float EquipmentLevelLuck = 0.1f;
		[Min(0f), Tooltip("처치 바닥 아이템 확률 x (1 + 행운 x 이 값)")] public float FloorItemLuckScale = 1f;
		[Range(0f, 1f), Tooltip("행운 1 당 레벨업 선택지가 늘어날 확률. 0이면 끔. 연결 자리: 카드 제시 장수")] public float ExtraChoiceChancePerLuck = 0f;
		[Min(0), Tooltip("행운으로 늘어나는 선택지 장수")] public int ExtraChoiceCount = 1;

		public float RarityLuckMultiplier(float permanentLuck, int equipmentLevel) => 1f + (permanentLuck + equipmentLevel * EquipmentLevelLuck) * RarityLuckScale;
		public float FloorItemLuckMultiplier(float permanentLuck) => 1f + permanentLuck * FloorItemLuckScale;

		// 확률 0이면 굴림을 소비하지 않음. 고정 시드 판의 난수 순서 유지
		public int ChoiceCount(int baseCount, float permanentLuck, Func<float> roll)
		{
			float chance = permanentLuck * ExtraChoiceChancePerLuck;
			if (chance <= 0f)
				return baseCount;
			return roll() < chance ? baseCount + ExtraChoiceCount : baseCount;
		}

		[Header("Threat levels (provisional values)")]
		[Tooltip("임시값. 한 줄이 한 단계. 줄 추가로 단계 추가, 단계 0은 줄 없음")] public List<DungeonThreatLevel> ThreatLevels = new();
	}
}
