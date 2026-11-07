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

		[Header("Random mode")]
		[Tooltip("판 시작 무기. 자동 공격이 없으면 경험치를 못 얻음")] public EquipmentData RandomModeStartEquipment;
		[Min(1), Tooltip("장비 칸 수. 시작 무기 포함, 인형 고유 장비 제외")] public int RandomModeSlotCount = 6;
		[Min(0), Tooltip("레벨업 한 번에 제시하는 새 장비 수")] public int RandomModeNewOfferCount = 1;

		[Header("Evolution")]
		[Tooltip("덱에 넣지 않는 카드. 조건은 CardData.Requirements, 지급은 상자 첫 보상")] public List<CardData> EvolutionCards = new();
		[Tooltip("트리 시작 카드를 차단하려 할 때 문구")] public string BanRootDeniedText = "시작 카드는 차단할 수 없어요.";

		[Header("Wave density (provisional values)")]
		[Min(0), Tooltip("임시값. 동시 적 수가 이 값 이상이면 주기 생성과 보충 중지. 0이면 상한 없음")] public int EnemySoftCap = 300;
		[Min(0), Tooltip("임시값. 동시 적 수가 이 값 이상이면 무리 사건도 중지. 보스와 정예는 예외. 0이면 상한 없음")] public int EnemyHardCap = 500;
		[Min(0), Tooltip("임시값. 최소 적 수 보충 한 번에 만들 최대 마릿수. 0이면 부족분 전부")] public int RefillBurstMax = 30;

		[Header("Threat levels (provisional values)")]
		[Tooltip("임시값. 한 줄이 한 단계. 줄 추가로 단계 추가, 단계 0은 줄 없음")] public List<DungeonThreatLevel> ThreatLevels = new();
	}
}
