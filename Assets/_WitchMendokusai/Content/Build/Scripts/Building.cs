using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "B_", menuName = "Variable/" + nameof(Building))]
	public class Building : DataSO
	{
		[field: Header("_" + nameof(Building))]
		[PropertyOrder(10)][field: SerializeField] public BuildingType Type { get; private set; } = new ();
		[PropertyOrder(11)][field: SerializeField] public int Cost { get; private set; }
		[PropertyOrder(12)][field: SerializeField] public Unit Mascot { get; private set; }
	}
}