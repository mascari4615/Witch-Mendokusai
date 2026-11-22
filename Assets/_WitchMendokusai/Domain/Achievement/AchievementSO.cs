using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 업적 하나. 퀘스트와 무관한 기록 (퀘스트는 할 일, 업적은 이미 한 일의 기념).
	///
	/// - 재는 값 하나 (기존 조건 종류: 아이템 수, 게임 통계, 던전 통계) 와 단계별 목표값 1~3개
	/// - Name 은 이름, Description 은 한 줄 사연, TierMedals 는 단계마다 메달 그림 (동, 은, 금)
	/// - 숨김 업적은 달성 전에 이름, 사연, 조건이 안 보임
	/// </summary>
	[CreateAssetMenu(fileName = "ACH_", menuName = "WM/Achievement/" + nameof(AchievementSO))]
	public class AchievementSO : DataSO
	{
		public const int NO_LEGACY_QUEST = -1;

		[field: Header("_" + nameof(AchievementSO))]
		[PropertyOrder(100)][field: SerializeField] public AchievementCategorySO Category { get; private set; }
		[PropertyOrder(101)][field: SerializeField, Tooltip("달성 전에는 실루엣과 물음표만")] public bool Hidden { get; private set; }
		[PropertyOrder(102)][field: SerializeField, Tooltip("조건 글. 목표값 없이 행동만. 숫자는 단계 칸이 보여 줌")] public string Condition { get; private set; }
		[PropertyOrder(103)][field: SerializeField] public CriteriaType MeasureType { get; private set; }
		[PropertyOrder(104)][field: SerializeField, Tooltip("재는 대상 (GameStatData, ItemData 등)")] public DataSO MeasureData { get; private set; }
		[PropertyOrder(105)][field: SerializeField, Tooltip("단계별 목표값. 오름차순, 1~3개")] public List<int> TierTargets { get; private set; } = new();
		[PropertyOrder(106)][field: SerializeField, Tooltip("업적이 퀘스트였던 시절 세이브에서 옮겨 올 퀘스트 ID. 없으면 -1")] public int LegacyQuestId { get; private set; } = NO_LEGACY_QUEST;
		[PropertyOrder(107)][field: SerializeField, Tooltip("그 퀘스트를 끝냈으면 몇 단계로 옮길지")] public int LegacyTier { get; private set; } = 1;
		[PropertyOrder(108)][field: SerializeField, Tooltip("단계마다 메달 그림. 1단계부터. 단계 수보다 적으면 마지막 그림을 이어 씀")] public List<Sprite> TierMedals { get; private set; } = new();

		public int MaxTier => TierTargets.Count;

		/// <summary>그 단계의 메달 그림. 그림이 단계보다 적으면 마지막 그림. 0단계는 쓰지 않음 (빈 칸은 분류의 EmptySlot)</summary>
		public Sprite MedalOf(int tier)
		{
			if (tier <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(tier), $"{name}: 0단계에는 메달이 없음");
			}
			return TierMedals[Math.Min(tier, TierMedals.Count) - 1];
		}

		/// <summary>로드 때 검사. 메달 그림이 1~단계 수 장이고 빈 칸이 없는지. 틀리면 던짐 (화면에 빈 메달이 조용히 뜨지 않게)</summary>
		public void ValidateMedals()
		{
			if (TierMedals.Count == 0 || TierMedals.Count > MaxTier || TierMedals.Contains(null))
			{
				throw new InvalidOperationException($"{name}: 메달 그림은 1~{MaxTier}장, 빈 칸 없이 ({TierMedals.Count}장)");
			}
			if (Category.EmptySlot == null)
			{
				throw new InvalidOperationException($"{Category.name}: 빈 칸 메달 (EmptySlot) 비어 있음");
			}
		}

		/// <summary>재는 값을 읽을 조건. 비교와 목표는 단계가 맡아서 여기서는 종류와 대상만</summary>
		public CriteriaInfo ToMeasureCriteria() => new() { Type = MeasureType, Data = MeasureData };
	}
}
