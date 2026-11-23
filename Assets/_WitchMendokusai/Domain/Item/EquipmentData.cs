using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum EquipmentType
	{
		Default,
		Pickaxe,
		Axe,
		FishingRod,

		// 마도 괭이 (TASK-WM-410) — 땅을 가는 손. 등급이 오르면 같은 밭을 덜 지치고 빨리 판다.
		Hoe,

		// 공격 오브젝트가 없는 보조 장비 (커피, 작은 망치 등). 채집 도구와 달리 무작위 장비 모드 후보
		Tool = 5
	}

	[CreateAssetMenu(fileName = nameof(EquipmentData), menuName = "WM/Variable/" + nameof(EquipmentData))]
	public class EquipmentData : ItemData
	{
		[field: Header("_" + nameof(EquipmentData))]
		[PropertyOrder(20)][field: SerializeField] public List<CardData> EffectCards { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public List<EffectInfo> Effects { get; private set; }
		[PropertyOrder(22)][field: SerializeField] public GameObject Object { get; private set; }
		[PropertyOrder(23)][field: SerializeField] public EquipmentType EquipmentType { get; private set; } = EquipmentType.Default;
		[PropertyOrder(26)][field: SerializeField, Tooltip("수선이 이 장비를 강하게 할 때 올리는 스탯 (보통 피해 증가). 비면 수선 대상 아님")] public List<UnitStatType> MendStats { get; private set; } = new();
		[PropertyOrder(27)][field: SerializeField, Tooltip("이 장비가 주는 수선 (반짇고리). 실 카드를 받아야 돎. 없으면 null")] public MendData Mend { get; private set; }
		[PropertyOrder(28)][field: SerializeField, Tooltip("이 장비 트리의 레벨 기준 성장 카드 (알리사 정돈된 손 등). 없으면 null")] public LevelGrowthData LevelGrowth { get; private set; }

		[PropertyOrder(25)][field: SerializeField] public SurvivalUIKind UIKind { get; private set; } = SurvivalUIKind.Count;

		[Tooltip("이 도구로 하는 행동의 대가 배율 (TASK-WM-410). 1 = 맨손과 같음, 0.6 = 시간·기운 60%. 씨앗 같은 자원은 안 줄어든다.")]
		[PropertyOrder(24)][field: SerializeField, Min(0.05f)] public float ActCostScale { get; private set; } = 1f;

		public bool IsRunOfferType => EquipmentType == EquipmentType.Default || EquipmentType == EquipmentType.Tool;
	}
}