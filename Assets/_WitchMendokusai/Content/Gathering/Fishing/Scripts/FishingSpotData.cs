using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(FishingSpotData), menuName = "WM/Variable/Gathering/" + nameof(FishingSpotData))]
	public class FishingSpotData : ScriptableObject
	{
		[field: Header("_" + nameof(FishingSpotData))]
		[field: SerializeField] public List<DataSOWithPercentage> Loots { get; private set; }
		[field: SerializeField, Min(0f)] public float BiteDelayMin { get; private set; } = 3f;
		[field: SerializeField, Min(0f)] public float BiteDelayMax { get; private set; } = 7f;
		[field: SerializeField, Min(0f), Tooltip("입질 후 입력 허용 시간 (초)")] public float InputWindow { get; private set; } = 1.2f;
	}
}
