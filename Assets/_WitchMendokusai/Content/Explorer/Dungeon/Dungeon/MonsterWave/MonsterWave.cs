using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[System.Serializable]
	public class MonsterWave
	{
		[field: Header("_" + nameof(MonsterWave))]
		[field: SerializeField] public Monster[] Monsters { get; set; }
		[field: SerializeField, Tooltip("Seconds"), Min(0)] public float StartTime { get; set; } = 0;
		[field: SerializeField, Tooltip("Seconds"), Min(0)] public float EndTime { get; set; } = 3600;
		[field: SerializeField, Tooltip("Seconds"), Min(0.1f)] public float SpawnDelay { get; set; } = .2f;
		[field: SerializeField] public bool Once { get; set; } = false;
	}
}