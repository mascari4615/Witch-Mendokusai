using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도 식물 도감 카테고리 (TASK-WM-167 Phase 1f+). 모든 WitchPlantSO 를 나열하고,
	/// 각 식물이 「표본으로 채집됐는가(DataManager.SpecimenCollected)」를 디테일에 보여준다 —
	/// 「봐줘야 진짜가 된다」: 관찰→개화→수확된 식물만 영구 표본으로 도감에 남는다(수확해 사라져도 영원).
	///
	/// ItemCodexCategory 패턴. 작물 .asset 미존재(Grey Box) 시 빈 목록(throw X — TryGetValue 가드).
	/// 현재 Codex 는 전체 노출(unlock 레이어 deferred) → 표본 여부는 BuildDetail 텍스트로 구분.
	/// </summary>
	public class PlantCodexCategory : IEntryProvider
	{
		public string Id => "plant";
		public string DisplayName => "마도 식물";
		public Sprite Icon => null;
		public IReadOnlyList<string> SubGroups => null;

		private readonly List<EntryDescriptor> entries = new();

		public void OnActivate()
		{
			entries.Clear();

			// 작물 .asset 이 하나도 없으면(Grey Box) 타입 키 자체가 없다 → TryGetValue 로 안전(throw X).
			if (SOManagerBridge.DataSOs.TryGetValue(typeof(WitchPlantSO), out Dictionary<int, DataSO> plants))
			{
				foreach (DataSO dataSO in plants.Values)
				{
					if (dataSO is WitchPlantSO plant == false)
					{
						continue;
					}

					entries.Add(new EntryDescriptor(
						id: $"P_{plant.ID}",
						displayName: plant.Name,
						icon: plant.Sprite,
						source: plant));
				}
			}

			entries.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
		}

		public void OnDeactivate() => entries.Clear();

		public IReadOnlyList<EntryDescriptor> GetEntries() => entries;

		public VisualElement BuildDetail(EntryDescriptor entry)
		{
			VisualElement detail = new();

			if (entry.Source is WitchPlantSO plant == false)
			{
				return detail;
			}

			// 「봐줘야 진짜」 — 이 식물을 관찰해 수확한 적이 있으면 영구 표본으로 남는다.
			bool collected = false;
			if (DataManager.TryGetExistingInstance(out DataManager dataManager))
			{
				dataManager.SpecimenCollected.TryGetValue(plant.ID, out collected);
			}

			Label specimenLabel = new(collected
				? "✓ 표본으로 남음 — 네가 봐준 덕에 진짜가 됐다."
				: "아직 채집 전 — 자랄 때 관찰(witness)하고 수확하면 영원히 남는다.");
			specimenLabel.style.whiteSpace = WhiteSpace.Normal;
			detail.Add(specimenLabel);

			Label careLabel = new(plant.DrainPerMinute > 0f
				? $"마도 작물 — 돌보지 않으면 시든다 (분당 생기 -{plant.DrainPerMinute})."
				: "코지 작물 — 방치해도 시들지 않는다.");
			careLabel.style.whiteSpace = WhiteSpace.Normal;
			detail.Add(careLabel);

			return detail;
		}
	}
}
