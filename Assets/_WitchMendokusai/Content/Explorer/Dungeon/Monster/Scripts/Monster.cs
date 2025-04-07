using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace WitchMendokusai
{
	public enum MonsterType
	{
		Normal,
		Boss,
	}

	public enum MonsterTag
	{
		Normal,
		Boss,
	}

	public enum MonsterEliteType
	{
		// 불타는, 과부하, 
	}

	[CreateAssetMenu(fileName = nameof(Monster), menuName = "Variable/" + nameof(Unit) + "/" + nameof(Monster))]
	public class Monster : Unit
	{
		[field: Header("_" + nameof(Monster))]
		[PropertyOrder(20)][field: SerializeField] public MonsterType Type { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public List<DataSOWithPercentage> Loots { get; private set; }
	}
}