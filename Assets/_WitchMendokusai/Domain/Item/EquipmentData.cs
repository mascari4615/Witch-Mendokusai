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
		Hoe
	}

	[CreateAssetMenu(fileName = nameof(EquipmentData), menuName = "WM/Variable/" + nameof(EquipmentData))]
	public class EquipmentData : ItemData
	{
		[field: Header("_" + nameof(EquipmentData))]
		[PropertyOrder(20)][field: SerializeField] public List<CardData> EffectCards { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public List<EffectInfo> Effects { get; private set; }
		[PropertyOrder(22)][field: SerializeField] public GameObject Object { get; private set; }
		[PropertyOrder(23)][field: SerializeField] public EquipmentType EquipmentType { get; private set; } = EquipmentType.Default;

		[Tooltip("이 도구로 하는 행동의 대가 배율 (TASK-WM-410). 1 = 맨손과 같음, 0.6 = 시간·기운 60%. 씨앗 같은 자원은 안 줄어든다.")]
		[PropertyOrder(24)][field: SerializeField, Min(0.05f)] public float ActCostScale { get; private set; } = 1f;
	}
}