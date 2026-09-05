using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Discovery;

namespace WitchMendokusai
{
	/// <summary>
	/// 마도 식물 도감 카테고리 (TASK-WM-167 Phase 1f+). 모든 WitchPlantSO 를 나열하고,
	/// 각 식물이 「표본으로 채집됐는가(DataManager.SpecimenCollected)」를 디테일에 보여준다 —
	/// 「봐줘야 진짜가 된다」: 관찰→개화→수확된 식물만 영구 표본으로 도감에 남는다(수확해 사라져도 영원).
	///
	/// ItemDiscoveryCategory 패턴. 작물 .asset 미존재(Grey Box) 시 빈 목록(throw X — TryGetValue 가드).
	/// 해금은 `DiscoveryUnlocks` 에 묻는다. 출처는 `PlantSpecimenUnlockSource` (표본으로 남은 것만 열림).
	/// </summary>
	public class PlantDiscoveryCategory : IEntryProvider
	{
		public const string CATALOG_ID = "plant";

		private const string ENTRY_ID_PREFIX = "P_";

		public string Id => CATALOG_ID;
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

					string entryId = ToEntryId(plant.ID);

					entries.Add(new EntryDescriptor(
						id: entryId,
						displayName: plant.Name,
						icon: plant.Sprite,
						source: plant,
						isUnlocked: DiscoveryUnlocks.IsUnlocked(Id, entryId)));
				}
			}

			entries.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
		}

		public void OnDeactivate() => entries.Clear();

		/// <summary>식물 데이터 ID 를 도감 항목 ID 로. 해금 출처가 되돌리려면 <see cref="TryParseEntryId"/>.</summary>
		public static string ToEntryId(int plantDataId) => ENTRY_ID_PREFIX + plantDataId;

		/// <summary>도감 항목 ID 에서 식물 데이터 ID 를 되돌린다. 모양이 다르면 false.</summary>
		public static bool TryParseEntryId(string entryId, out int plantDataId)
		{
			plantDataId = 0;
			if (entryId == null || entryId.StartsWith(ENTRY_ID_PREFIX, StringComparison.Ordinal) == false)
			{
				return false;
			}

			return int.TryParse(entryId.Substring(ENTRY_ID_PREFIX.Length), out plantDataId);
		}

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
