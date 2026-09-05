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

		/// <summary>
		/// 이 씨앗이 <b>무엇으로 자라는가</b> (TASK-WM-410). 채워져 있으면 갈린 밭에 심을 때
		/// 실시간 초(<see cref="GrowSeconds"/>)가 아니라 <b>게임 시간·생기·돌봄</b> 규칙으로 자란다.
		///
		/// ★ 왜 씨앗이 들고 있나: 「무엇으로 자라는지」는 씨앗의 성질이지 밭의 성질이 아니다.
		///   밭이 들면 밭마다 심을 수 있는 작물이 박히고, 씨앗을 옮겨 심을 수 없게 된다.
		/// null = 옛 경로(빈 땅 entity 심기 / 실시간 초 밭)만 쓴다.
		/// </summary>
		[PropertyOrder(33)][field: SerializeField] public WitchPlantSO Plant { get; private set; }

		/// <summary>검증·에디터 도구가 작물을 물린다 (WitchPlantSO.EditorSetHarvestLoots 선례).</summary>
		public void EditorSetPlant(WitchPlantSO plant) => Plant = plant;
	}
}
