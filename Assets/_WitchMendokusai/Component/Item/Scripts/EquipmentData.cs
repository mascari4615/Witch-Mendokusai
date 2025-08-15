using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(EquipmentData), menuName = "WM/Variable/" + nameof(EquipmentData))]
	public class EquipmentData : ItemData
	{
		[field: Header("_" + nameof(EquipmentData))]
		[PropertyOrder(20)][field: SerializeField] public List<CardData> EffectCards { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public List<EffectInfo> Effects { get; private set; }
		[PropertyOrder(22)][field: SerializeField] public GameObject Object { get; private set; }
	}
}