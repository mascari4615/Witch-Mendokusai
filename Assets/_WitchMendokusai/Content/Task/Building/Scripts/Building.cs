using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum BuildingType
	{
		Building = 0,
		Decoration = 1,
		Util = 2,
	}

	[CreateAssetMenu(fileName = "BD_", menuName = "Variable/" + nameof(Building))]
	public class Building : DataSO
	{
		[field: Header("_" + nameof(Building))]
		[PropertyOrder(10)][field: SerializeField] public BuildingType Type { get; private set; } = BuildingType.Building;
		[PropertyOrder(11)][field: SerializeField] public int Cost { get; private set; } = 0;
		[PropertyOrder(12)][field: SerializeField] public GameObject Prefab { get; private set; } = null;
		[PropertyOrder(13)][field: SerializeField] public Unit Mascot { get; private set; } = null;
		[PropertyOrder(14)][field: SerializeField] public Vector2Int Size { get; private set; } = new(1, 1); // 당장은 정사각형 모양에 대해서만 고려 - 250317. 1256
	}
}