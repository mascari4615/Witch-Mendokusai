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
		[field: SerializeField, Tooltip("웨이브가 켜진 동안 유지할 최소 적 수. 0이면 보충 없음. 임시값"), Min(0)] public int MinOnScreen { get; set; } = 0;
	}
}