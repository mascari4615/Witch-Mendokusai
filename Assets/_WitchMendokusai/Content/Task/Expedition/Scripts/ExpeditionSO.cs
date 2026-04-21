using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "EX_", menuName = "WM/Variable/ExpeditionSO")]
	public class ExpeditionSO : DataSO
	{
		[field: Header("_" + nameof(ExpeditionSO))]
		[field: SerializeField] public float DurationSeconds { get; private set; } = 180f;
		[field: SerializeField] public List<DataSOWithPercentage> Loot { get; private set; } = new();
	}
}
