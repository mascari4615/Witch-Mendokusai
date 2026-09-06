using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	/// <summary>접속한 사람 하나 — 서버가 아는 것은 이만큼이다 (TASK-WM-216).</summary>
	public sealed class WorldDoll
	{
		/// <summary>가방 칸 수 — 게임 쪽 기본값과 같은 30.</summary>
		public const int BAG_SLOTS = 30;

		private readonly List<Item> slots = new List<Item>();

		public WorldDoll(int id, Vector3 position)
		{
			Id = id;
			Position = position;

			for (int i = 0; i < BAG_SLOTS; i++)
				slots.Add(null);

			// 가방 규칙은 게임과 같은 것을 그대로 쓴다 (TASK-WM-215 에서 판정 층으로 내린 그것).
			Bag = new InventoryCore(slots, BAG_SLOTS);
		}

		public int Id { get; }

		/// <summary>이 인형의 주인 (TASK-WM-218). 0 = 아직 아무도 아님(옛 방식).</summary>
		public int IdentityId { get; set; }

		public Vector3 Position { get; set; }
		public InventoryCore Bag { get; }

		/// <summary>이 사람의 몸 (TASK-WM-251). 0 이면 쓰러진 것이다.</summary>
		public int Health { get; set; } = Net.StrikeRule.FULL_HEALTH;

		/// <summary>마지막으로 때린 시각 (ms) — 얼마나 자주 때리나를 세계가 본다.</summary>
		public long LastStruckMs { get; set; }

		/// <summary>
		/// 옆 세계에서 <b>빌려 온 이름</b> (TASK-WM-263) — 국경 너머 그림자만 쓴다.
		/// 이 세계의 사람은 늘 비어 있다(이름은 이 세계의 신원부에서 온다).
		/// </summary>
		public string BorrowedName { get; set; } = string.Empty;

		/// <summary>가방을 <b>비운다</b> (TASK-WM-259) — 통행증이 진실인 자리에서 옛 것을 걷어낸다.</summary>
		public void EmptyBag()
		{
			foreach (BagSaveEntry held in SaveBag())
				Bag.Consume(held.itemId, held.amount);
		}

		/// <summary>가방을 뜬다 — 종류별 개수만(칸 배치는 세계의 관심사가 아니다).</summary>
		public List<BagSaveEntry> SaveBag()
		{
			Dictionary<int, int> counts = new Dictionary<int, int>();
			for (int i = 0; i < slots.Count; i++)
			{
				Item item = slots[i];
				if (item == null || item.Data == null)
					continue;

				counts.TryGetValue(item.Data.ID, out int had);
				counts[item.Data.ID] = had + item.Amount;
			}

			List<BagSaveEntry> saved = new List<BagSaveEntry>();
			foreach (KeyValuePair<int, int> entry in counts)
				saved.Add(new BagSaveEntry { itemId = entry.Key, amount = entry.Value });

			return saved;
		}
	}
}

