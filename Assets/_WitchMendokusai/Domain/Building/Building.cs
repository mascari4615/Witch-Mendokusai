using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "BD_", menuName = "WM/Variable/" + nameof(Building))]
	public class Building : DataSO
	{
		[field: Header("_" + nameof(Building))]
		[PropertyOrder(10)][field: SerializeField] public BuildingType Type { get; private set; } = BuildingType.Building;
		[PropertyOrder(11)][field: SerializeField] public int Cost { get; private set; } = 0;
		[PropertyOrder(12)][field: SerializeField] public GameObject Prefab { get; private set; } = null;
		[PropertyOrder(13)][field: SerializeField] public Unit Mascot { get; private set; } = null;
		[PropertyOrder(14)][field: SerializeField] public Vector2Int Size { get; private set; } = new(1, 1); // 당장은 정사각형 모양에 대해서만 고려 - 250317. 1256

		/// <summary>
		/// 물건을 넣어 둘 수 있는 칸 수 (TASK-WM-217 후속). 0 = 상자가 아니다.
		/// 세계가 이 값으로 상자를 놓는다 — 내가 넣은 걸 친구가 꺼낸다.
		/// </summary>
		[PropertyOrder(15)][field: SerializeField] public int StorageSlots { get; private set; } = 0;
	}
}