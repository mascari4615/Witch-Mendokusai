using UnityEngine;

namespace WitchMendokusai
{
	[System.Serializable]
	public class ResourceNodeWave
	{
		[field: Header("_" + nameof(ResourceNodeWave))]
		[field: SerializeField] public ResourceNode[] ResourceNodes { get; set; }
		[field: SerializeField] public int MaxNodeCount { get; set; } = 10;
		[field: SerializeField, Tooltip("Seconds"), Min(0)] public float RespawnDelay { get; set; } = 5f;
	}
}
