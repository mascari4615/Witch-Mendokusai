using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(SeedItemData), menuName = "WM/Variable/" + nameof(SeedItemData))]
	public class SeedItemData : ItemData
	{
		[field: Header("_" + nameof(SeedItemData))]
		[PropertyOrder(30)][field: SerializeField] public List<DataSOWithPercentage> HarvestLoots { get; private set; } = new();
		[PropertyOrder(31)][field: SerializeField, Min(1f)] public float GrowSeconds { get; private set; } = 30f;
	}
}
