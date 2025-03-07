using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(MonsterWave), menuName = "Variable/MonsterWave")]
	public class MonsterWave : DataSO
	{
		[field: Header("_" + nameof(MonsterWave))]
		[field: SerializeField] public Monster[] Monsters { get; set; }
		[field: SerializeField] public float StartTime { get; set; } = 0;
		[field: SerializeField] public float EndTime { get; set; } = 3600;
		[field: SerializeField] public float SpawnDelay { get; set; } = .2f;
		[field: SerializeField] public bool Once { get; set; }
	}
}