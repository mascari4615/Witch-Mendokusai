using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai.Server
{
	/// <summary>접속한 사람 하나 — 서버가 아는 것은 이만큼이다 (TASK-WM-216).</summary>
	public sealed class Doll
	{
		/// <summary>가방 칸 수 — 게임 쪽 기본값과 같은 30.</summary>
		public const int BAG_SLOTS = 30;

		private readonly List<Item> slots = new List<Item>();

		public Doll(int id, Vector3 position)
		{
			Id = id;
			Position = position;

			for (int i = 0; i < BAG_SLOTS; i++)
				slots.Add(null);

			// 가방 규칙은 게임과 같은 것을 그대로 쓴다 (TASK-WM-215 에서 판정 층으로 내린 그것).
			Bag = new InventoryCore(slots, BAG_SLOTS);
		}

		public int Id { get; }
		public Vector3 Position { get; set; }
		public InventoryCore Bag { get; }
	}

	/// <summary>세워진 건물 하나 — 서버가 기억하는 최소 (TASK-WM-216).</summary>
	public sealed class PlacedBuilding
	{
		public PlacedBuilding(Vector3Int pivot, Vector2Int size, int buildingId)
		{
			Pivot = pivot;
			Size = size;
			BuildingId = buildingId;
		}

		public Vector3Int Pivot { get; }
		public Vector2Int Size { get; }
		public int BuildingId { get; }
	}

	/// <summary>
	/// 서버가 굴리는 세계 — <b>판정만</b> 있고 화면은 없다 (TASK-WM-216).
	///
	/// 여기 있는 규칙은 게임과 같은 것을 쓴다(좌표·수학 = DomainSDK).
	/// 「어떻게 보이나」는 각 창(Unity · 웹)이 알아서 한다.
	/// </summary>
	public sealed class World
	{
		/// <summary>한 번 움직임에 갈 수 있는 거리 상한 — 순간이동 방지(서버 권위의 최소선).</summary>
		public const float MAX_STEP = 1.5f;

		// ★ 여러 갈래가 동시에 만진다 (TASK-WM-216): 접속·퇴장은 각 연결의 흐름에서, 훑기는 알림 루프에서.
		//   자물쇠 없이 두었더니 알림 루프가 훑는 도중 목록이 바뀌어 **터졌다**(NullReference).
		//   화면 없는 서버라 터져도 티가 안 난다 — 그래서 상태를 만지는 자리를 전부 한 자물쇠 아래 둔다.
		private readonly object gate = new object();
		private readonly Dictionary<int, Doll> dolls = new Dictionary<int, Doll>();
		private readonly Dictionary<Vector3Int, int> occupiedCells = new Dictionary<Vector3Int, int>();
		private readonly List<PlacedBuilding> placed = new List<PlacedBuilding>();
		private int nextId = 1;

		/// <summary>훑을 때는 <b>그 순간의 사본</b>을 준다 — 훑는 동안 목록이 바뀌어도 안전하다.</summary>
		public Doll[] Snapshot()
		{
			lock (gate)
			{
				Doll[] copy = new Doll[dolls.Count];
				dolls.Values.CopyTo(copy, 0);
				return copy;
			}
		}

		public Doll Join()
		{
			lock (gate)
			{
				Doll doll = new Doll(nextId++, Vector3.zero);
				dolls[doll.Id] = doll;
				return doll;
			}
		}

		public void Leave(int dollId)
		{
			lock (gate)
			{
				dolls.Remove(dollId);
			}
		}

		/// <summary>
		/// 움직임 요청을 <b>서버가 판정한다.</b> 클라가 보낸 값을 그대로 믿지 않는다 —
		/// 한 번에 갈 수 있는 거리로 잘라낸다(믿으면 순간이동이 공짜가 된다).
		/// </summary>
		/// <summary>
		/// 짓기 요청을 <b>서버가 판정한다</b> — 겹치면 거절.
		/// 겹침 규칙은 게임과 같은 것(<see cref="BuildingFootprint"/>)을 쓴다.
		/// </summary>
		public bool TryPlaceBuilding(Vector3Int pivot, Vector2Int size, int buildingId)
		{
			lock (gate)
			{
				HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(occupiedCells.Keys);
				if (BuildingFootprint.IsBlocked(pivot, size, occupied))
					return false;

				List<Vector3Int> cells = BuildingFootprint.Cells(pivot, size);
				for (int i = 0; i < cells.Count; i++)
					occupiedCells[cells[i]] = buildingId;

				placed.Add(new PlacedBuilding(pivot, size, buildingId));
				return true;
			}
		}

		/// <summary>
		/// 줍기 — 서버가 가방 규칙으로 넣는다. <b>못 넣고 남은 개수</b>를 돌려준다(가방이 꽉 찼을 때).
		/// </summary>
		public int TryGather(int dollId, int itemId, int amount)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out Doll doll) == false)
					return amount;

				IItemData itemData = ServerItemCatalog.Find(itemId);
				if (itemData == null)
					return amount;

				return doll.Bag.Add(itemData, amount);
			}
		}

		/// <summary>그 인형이 그 아이템을 몇 개 가졌나.</summary>
		public int BagCount(int dollId, int itemId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out Doll doll) ? doll.Bag.CountById(itemId) : 0;
			}
		}

		/// <summary>제작 등으로 재료를 쓴다. 못 쓰고 남은 개수를 돌려준다.</summary>
		public int TryConsume(int dollId, int itemId, int amount)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out Doll doll) ? doll.Bag.Consume(itemId, amount) : amount;
			}
		}

		/// <summary>세워진 건물들 — 훑는 동안 바뀌어도 안전하게 사본으로.</summary>
		public PlacedBuilding[] Buildings()
		{
			lock (gate)
			{
				return placed.ToArray();
			}
		}

		/// <summary>어느 건물이 몇 개 서 있나 — 세는 규칙도 게임과 같은 것.</summary>
		public int CountBuildings(int buildingId)
		{
			lock (gate)
			{
				List<BuildingInstanceData> instances = new List<BuildingInstanceData>();
				for (int i = 0; i < placed.Count; i++)
					instances.Add(new BuildingInstanceData(placed[i].BuildingId));

				return BuildingCensus.CountById(instances, buildingId);
			}
		}

		public bool TryMove(int dollId, Vector3 delta)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out Doll doll) == false)
					return false;

				Vector3 clamped = Vector3.ClampMagnitude(delta, MAX_STEP);
				doll.Position = doll.Position + clamped;
				return true;
			}
		}
	}
}
