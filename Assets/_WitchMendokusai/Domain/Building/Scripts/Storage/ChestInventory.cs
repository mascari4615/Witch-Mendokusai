using System.Collections.Generic;
using Newtonsoft.Json;

namespace WitchMendokusai
{
	// 보관 상자 1개의 내용물 (per-instance). itemID -> count.
	// 영속 = BuildingInstanceData.RuntimeData(string JSON) 직렬화 → GridData 세이브 편승.
	// WorldStageSaveData / WorldStage 무수정 (TASK-WM-169 Phase 1 — RuntimeData seam).
	public class ChestInventory
	{
		private readonly Dictionary<int, int> itemCounts = new();

		public IReadOnlyDictionary<int, int> ItemCounts => itemCounts;

		public void Add(int itemID, int count = 1)
		{
			if (count <= 0)
				return;

			if (itemCounts.ContainsKey(itemID))
				itemCounts[itemID] += count;
			else
				itemCounts[itemID] = count;
		}

		public bool Remove(int itemID, int count = 1)
		{
			if (count <= 0)
				return false;

			if (itemCounts.TryGetValue(itemID, out int current) == false || current < count)
				return false;

			int next = current - count;
			if (next <= 0)
				itemCounts.Remove(itemID);
			else
				itemCounts[itemID] = next;

			return true;
		}

		public int GetCount(int itemID) => itemCounts.TryGetValue(itemID, out int count) ? count : 0;

		// Newtonsoft 사용 (JsonUtility 는 Dictionary<,> 직렬화 불가). RuntimeData seam 에
		// FarmRuntimeData(JsonUtility)와 라이브러리 혼재하나, 각 building 타입이 자기 blob 만 파싱하므로 안전.
		public string ToJson() => JsonConvert.SerializeObject(itemCounts);

		public static ChestInventory FromJson(string json)
		{
			ChestInventory inventory = new();

			if (string.IsNullOrEmpty(json))
				return inventory;

			Dictionary<int, int> loaded;
			try
			{
				loaded = JsonConvert.DeserializeObject<Dictionary<int, int>>(json);
			}
			catch (JsonException)
			{
				// 손상/악의적 세이브 — 상자 1개 깨짐이 GridData 전체 로드를 죽이지 않게 빈 인벤토리 폴백.
				// (경고 로깅은 Unity 경계인 P1b 로드 seam 책임 — 본 POCO 는 순수 유지)
				return inventory;
			}

			if (loaded == null)
				return inventory;

			foreach (KeyValuePair<int, int> pair in loaded)
			{
				// 불변식 재강제: 정상 경로(Add/Remove)는 항상 > 0 만 저장 → 신뢰 못 할 세이브 입력도 동일 sanitize.
				if (pair.Value > 0)
					inventory.itemCounts[pair.Key] = pair.Value;
			}

			return inventory;
		}
	}
}
