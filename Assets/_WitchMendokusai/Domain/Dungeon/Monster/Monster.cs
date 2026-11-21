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

	[CreateAssetMenu(fileName = nameof(Monster), menuName = "WM/Variable/" + nameof(Unit) + "/" + nameof(Monster))]
	public class Monster : Unit
	{
		[field: Header("_" + nameof(Monster))]
		[PropertyOrder(20)][field: SerializeField] public MonsterType Type { get; private set; }
		[PropertyOrder(21)][field: SerializeField] public List<DataSOWithPercentage> Loots { get; private set; }
		[PropertyOrder(22)][field: SerializeField, Tooltip("정면 외 위협 (원거리, 돌진, 장판). 비우면 추적만. FSMSlime 이 돌림")] public MonsterAttack Attack { get; private set; }
		[PropertyOrder(23)][field: SerializeField, Tooltip("그림 색 곱. 같은 프리팹을 쓰는 변종 구분. 정예가 되면 정예 색을 위에 곱함")] public Color Tint { get; private set; } = Color.white;
	}
}