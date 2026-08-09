using System;
using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>상자 한 칸이 기억하는 것 — 어느 자리에 무엇이 몇 개.</summary>
	[Serializable]
	public class StorageSaveEntry
	{
		public int x;
		public int y;
		public int z;
		public BagSaveEntry[] items = Array.Empty<BagSaveEntry>();
	}

	/// <summary>
	/// 세계에 <b>물건을 놓아 두는 자리</b> — 지은 상자 (TASK-WM-217 후속).
	///
	/// ★ 왜 세계인가: 상자가 창의 것이면 「내가 넣은 걸 남이 못 본다」 — 그건 같이 노는 게 아니다.
	///   세계가 갖고 있으면 <b>내가 넣고 친구가 꺼낸다</b>. MMO 에서 물건을 나누는 가장 단순한 길이다.
	///
	/// 규칙은 가방과 <b>같은 것</b>(<see cref="InventoryCore"/>)을 쓴다 — 칸이 차면 더 안 들어간다.
	/// </summary>
	public sealed class WorldStorages
	{
		/// <summary>상자에 손이 닿는 거리 — 이보다 멀면 못 연다(창이 우겨도).</summary>
		public const float REACH = 3f;

		private sealed class Box
		{
			public Box(int slots)
			{
				Slots = new List<Item>();
				for (int i = 0; i < slots; i++)
					Slots.Add(null);

				Bag = new InventoryCore(Slots, slots);
			}

			public List<Item> Slots { get; }
			public InventoryCore Bag { get; }
		}

		private readonly object gate = new object();
		private readonly Dictionary<Vector3Int, Box> boxes = new Dictionary<Vector3Int, Box>();

		/// <summary>상자 안이 바뀔 때마다 오르는 수 — 창이 「내 화면이 낡았나」를 이 수로 안다.</summary>
		public int Version { get; private set; }

		/// <summary>그 자리에 상자가 있나(세워질 때 만들어진다).</summary>
		public bool Has(Vector3Int cell)
		{
			lock (gate)
			{
				return boxes.ContainsKey(cell);
			}
		}

		/// <summary>그 자리에 상자를 놓는다 — 이미 있으면 그대로 둔다(안에 든 것을 지우면 안 된다).</summary>
		public void Place(Vector3Int cell, int slots)
		{
			if (slots < 1)
				return;

			lock (gate)
			{
				if (boxes.ContainsKey(cell))
					return;

				boxes[cell] = new Box(slots);
				Version++;
			}
		}

		/// <summary>
		/// 상자를 치운다. <b>안에 든 것은 사라진다</b> — 부수기 전에 창이 사람에게 물어야 한다.
		/// 있었으면 true.
		/// </summary>
		public bool Remove(Vector3Int cell)
		{
			lock (gate)
			{
				if (boxes.Remove(cell) == false)
					return false;

				Version++;
				return true;
			}
		}

		/// <summary>넣는다 — 못 넣고 남은 개수를 돌려준다(칸이 차면 남는다).</summary>
		public int Put(Vector3Int cell, IItemData item, int amount, float fromX, float fromZ)
		{
			if (item == null || amount <= 0)
				return amount;

			lock (gate)
			{
				if (WithinReach(cell, fromX, fromZ) == false)
					return amount;

				if (boxes.TryGetValue(cell, out Box box) == false)
					return amount;

				int leftover = box.Bag.Add(item, amount);
				if (leftover != amount)
					Version++;

				return leftover;
			}
		}

		/// <summary>꺼낸다 — 실제로 꺼낸 개수를 돌려준다(없으면 0).</summary>
		public int Take(Vector3Int cell, int itemId, int amount, float fromX, float fromZ)
		{
			if (amount <= 0)
				return 0;

			lock (gate)
			{
				if (WithinReach(cell, fromX, fromZ) == false)
					return 0;

				if (boxes.TryGetValue(cell, out Box box) == false)
					return 0;

				int had = box.Bag.CountById(itemId);
				if (had <= 0)
					return 0;

				int wanted = amount < had ? amount : had;
				int missing = box.Bag.Consume(itemId, wanted);
				int taken = wanted - missing;
				if (taken > 0)
					Version++;

				return taken;
			}
		}

		/// <summary>그 상자 안 — 종류별 개수. 상자가 없으면 빈 목록.</summary>
		public List<BagSaveEntry> Contents(Vector3Int cell)
		{
			List<BagSaveEntry> contents = new List<BagSaveEntry>();
			lock (gate)
			{
				if (boxes.TryGetValue(cell, out Box box) == false)
					return contents;

				AppendContents(box, contents);
				return contents;
			}
		}

		/// <summary>세계가 잠들었다 깨어나도 상자 안이 그대로여야 한다.</summary>
		public List<StorageSaveEntry> Save()
		{
			List<StorageSaveEntry> saved = new List<StorageSaveEntry>();
			lock (gate)
			{
				foreach (KeyValuePair<Vector3Int, Box> entry in boxes)
				{
					List<BagSaveEntry> contents = new List<BagSaveEntry>();
					AppendContents(entry.Value, contents);

					saved.Add(new StorageSaveEntry
					{
						x = entry.Key.x,
						y = entry.Key.y,
						z = entry.Key.z,
						items = contents.ToArray(),
					});
				}
			}

			return saved;
		}

		/// <summary>기억에서 되살린다 — 칸 수는 지금의 목록이 정한다(자산이 바뀌면 그쪽이 이긴다).</summary>
		public void Load(IEnumerable<StorageSaveEntry> saved, Func<Vector3Int, int> slotsOf, WorldItemCatalog catalog)
		{
			lock (gate)
			{
				boxes.Clear();
				Version++;

				if (saved == null)
					return;

				foreach (StorageSaveEntry entry in saved)
				{
					if (entry == null)
						continue;

					Vector3Int cell = new Vector3Int(entry.x, entry.y, entry.z);
					int slots = slotsOf == null ? 0 : slotsOf(cell);
					if (slots < 1)
						continue;

					Box box = new Box(slots);
					boxes[cell] = box;

					if (entry.items == null || catalog == null)
						continue;

					for (int i = 0; i < entry.items.Length; i++)
					{
						BagSaveEntry item = entry.items[i];
						if (item == null)
							continue;

						IItemData data = catalog.Find(item.itemId);
						if (data == null)
							continue;

						box.Bag.Add(data, item.amount);
					}
				}
			}
		}

		private bool WithinReach(Vector3Int cell, float fromX, float fromZ)
		{
			float dx = cell.x - fromX;
			float dz = cell.z - fromZ;
			return dx * dx + dz * dz <= REACH * REACH;
		}

		private static void AppendContents(Box box, List<BagSaveEntry> into)
		{
			Dictionary<int, int> counts = new Dictionary<int, int>();
			for (int i = 0; i < box.Slots.Count; i++)
			{
				Item item = box.Slots[i];
				if (item == null || item.Data == null)
					continue;

				counts.TryGetValue(item.Data.ID, out int had);
				counts[item.Data.ID] = had + item.Amount;
			}

			foreach (KeyValuePair<int, int> pair in counts)
				into.Add(new BagSaveEntry { itemId = pair.Key, amount = pair.Value });
		}
	}
}
