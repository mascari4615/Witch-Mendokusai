using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "DC_", menuName = "WM/Variable/" + nameof(DungeonConstraint))]
	public class DungeonConstraint : DataSO
	{
		[field: SerializeField] public List<DungeonConstraintEffectInfo> Effects { get; private set; } = new();
	}
}