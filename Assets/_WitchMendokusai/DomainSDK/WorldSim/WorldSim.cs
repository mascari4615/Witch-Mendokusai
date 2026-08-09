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
	/// 세계 하나 — <b>판정만</b> 있고 화면은 없다 (TASK-WM-216 → 217).
	///
	/// ★ 판정 층에 둔 이유 (TASK-WM-217): 「혼자 놀기」를 별도 모드로 만들지 않으려면
	///   게임 자신이 이 세계를 품고 돌 수 있어야 한다. 서버 프로세스를 따로 끼워 배포하는 대신,
	///   같은 클래스를 .NET 서버가 호스팅하거나 유니티가 자기 안에서 돌린다 — <b>코드는 한 벌</b>.
	///
	/// 여기 있는 규칙은 게임과 같은 것을 쓴다(좌표·수학·가방·건축 = DomainSDK).
	/// 「어떻게 보이나」는 각 창(Unity · 웹)이 알아서 한다.
	/// </summary>
	public sealed class WorldSim
	{
		/// <summary>한 번 움직임에 갈 수 있는 거리 상한 — 순간이동 방지(서버 권위의 최소선).</summary>
		public const float MAX_STEP = 1.5f;

		// ★ 여러 갈래가 동시에 만진다 (TASK-WM-216): 접속·퇴장은 각 연결의 흐름에서, 훑기는 알림 루프에서.
		//   자물쇠 없이 두었더니 알림 루프가 훑는 도중 목록이 바뀌어 **터졌다**(NullReference).
		//   화면 없는 서버라 터져도 티가 안 난다 — 그래서 상태를 만지는 자리를 전부 한 자물쇠 아래 둔다.
		private readonly object gate = new object();
		private readonly Dictionary<int, WorldDoll> dolls = new Dictionary<int, WorldDoll>();
		private readonly Dictionary<Vector3Int, int> occupiedCells = new Dictionary<Vector3Int, int>();
		private readonly List<PlacedBuilding> placed = new List<PlacedBuilding>();
		private int nextId = 1;

		/// <summary>훑을 때는 <b>그 순간의 사본</b>을 준다 — 훑는 동안 목록이 바뀌어도 안전하다.</summary>
		public WorldDoll[] Snapshot()
		{
			lock (gate)
			{
				WorldDoll[] copy = new WorldDoll[dolls.Count];
				dolls.Values.CopyTo(copy, 0);
				return copy;
			}
		}

		public WorldDoll Join()
		{
			lock (gate)
			{
				WorldDoll doll = new WorldDoll(nextId++, Vector3.zero);
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
		public int TryGather(int dollId, IItemData itemData, int amount)
		{
			if (itemData == null)
				return amount;

			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out WorldDoll doll) == false)
					return amount;

				return doll.Bag.Add(itemData, amount);
			}
		}

		/// <summary>그 인형이 그 아이템을 몇 개 가졌나.</summary>
		public int BagCount(int dollId, int itemId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.Bag.CountById(itemId) : 0;
			}
		}

		/// <summary>제작 등으로 재료를 쓴다. 못 쓰고 남은 개수를 돌려준다.</summary>
		public int TryConsume(int dollId, int itemId, int amount)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.Bag.Consume(itemId, amount) : amount;
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

		/// <summary>
		/// 세계의 기억을 뜬다 (TASK-WM-217 단계 5). 뜨는 동안 세계가 바뀌어도 안전하게 자물쇠 안에서.
		/// </summary>
		public WorldSaveData Save()
		{
			lock (gate)
			{
				BuildingSaveData[] saved = new BuildingSaveData[placed.Count];
				for (int i = 0; i < placed.Count; i++)
				{
					saved[i] = new BuildingSaveData
					{
						x = placed[i].Pivot.x,
						y = placed[i].Pivot.y,
						z = placed[i].Pivot.z,
						w = placed[i].Size.x,
						l = placed[i].Size.y,
						buildingId = placed[i].BuildingId,
					};
				}

				return new WorldSaveData { buildings = saved };
			}
		}

		/// <summary>
		/// 기억을 되살린다 (TASK-WM-217 단계 5). <b>지금 있는 건물은 지우고</b> 저장된 것으로 갈아끼운다.
		///
		/// 겹치는 건물은 <b>버린다</b> — 저장 파일이 망가졌거나 규칙이 바뀌었을 때
		/// 겹친 채로 되살리면 그 뒤로 짓기 판정이 영원히 이상해진다. 되살린 개수를 돌려준다.
		/// </summary>
		public int Load(WorldSaveData data)
		{
			lock (gate)
			{
				placed.Clear();
				occupiedCells.Clear();

				if (data == null || data.buildings == null)
					return 0;

				int restored = 0;
				for (int i = 0; i < data.buildings.Length; i++)
				{
					BuildingSaveData saved = data.buildings[i];
					if (saved == null)
						continue;

					Vector3Int pivot = new Vector3Int(saved.x, saved.y, saved.z);
					Vector2Int size = new Vector2Int(saved.w, saved.l);

					HashSet<Vector3Int> occupied = new HashSet<Vector3Int>(occupiedCells.Keys);
					if (BuildingFootprint.IsBlocked(pivot, size, occupied))
						continue;

					List<Vector3Int> cells = BuildingFootprint.Cells(pivot, size);
					for (int cell = 0; cell < cells.Count; cell++)
						occupiedCells[cells[cell]] = saved.buildingId;

					placed.Add(new PlacedBuilding(pivot, size, saved.buildingId));
					restored++;
				}

				return restored;
			}
		}

		public bool TryMove(int dollId, Vector3 delta)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out WorldDoll doll) == false)
					return false;

				Vector3 clamped = Vector3.ClampMagnitude(delta, MAX_STEP);
				doll.Position = doll.Position + clamped;
				return true;
			}
		}
	}
}
