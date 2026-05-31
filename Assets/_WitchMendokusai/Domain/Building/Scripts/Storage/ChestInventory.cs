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

		public string ToJson() => JsonConvert.SerializeObject(itemCounts);

		public static ChestInventory FromJson(string json)
		{
			ChestInventory inventory = new();

			if (string.IsNullOrEmpty(json))
				return inventory;

			Dictionary<int, int> loaded = JsonConvert.DeserializeObject<Dictionary<int, int>>(json);
			if (loaded == null)
				return inventory;

			foreach (KeyValuePair<int, int> pair in loaded)
				inventory.itemCounts[pair.Key] = pair.Value;

			return inventory;
		}
	}
}
