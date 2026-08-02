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

		/// <summary>
		/// 빈 땅에 심으면 spawn 될 voxel entity. null = 농사 작물 (밭 슬롯 전용, 빈 땅 심기 X).
		/// TASK-026 의 TreeSeedItemData 별도 클래스 폐기 — 본 필드 1줄로 흡수 (G4-4).
		/// </summary>
		[PropertyOrder(32)][field: SerializeField] public EntityData PlantedEntity { get; private set; }
	}
}
