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

		/// <summary>
		/// 세계의 시계 — <b>사람이 없어도 흐른다</b> (TASK-WM-217).
		/// 자릿수는 게임의 WorldClockSO 가 정본이고, 서버는 그 값을 받아 여기 꽂는다.
		/// </summary>
		public WorldCalendar Calendar { get; } = new WorldCalendar(24, 28, 4, 6, 0);

		/// <summary>모두가 같이 젓는 하나의 솥 — 호스트가 아니라 세계가 갖는다 (TASK-WM-217).</summary>
		public WorldCauldron Cauldron { get; } = new WorldCauldron();

		/// <summary>시간을 흘린다. 하루가 바뀌었으면 true.</summary>
		public bool AdvanceMinutes(float minutes)
		{
			lock (gate)
			{
				return Calendar.AdvanceMinutes(minutes);
			}
		}

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

		public WorldDoll Join() => Join(identityId: 0, catalog: null);

		/// <summary>
		/// 그 사람의 인형을 내준다 (TASK-WM-218). 전에 왔던 사람이면 <b>기억해 둔 자리·가방</b>으로,
		/// 처음이면 빈 인형으로. 신원이 0 이면 옛 방식(매번 새 인형)이다.
		/// </summary>
		public WorldDoll Join(int identityId, WorldItemCatalog catalog)
		{
			lock (gate)
			{
				WorldDoll doll = new WorldDoll(nextId++, Vector3.zero) { IdentityId = identityId };

				if (identityId != 0 && remembered.TryGetValue(identityId, out PersonSaveData kept))
				{
					doll.Position = new Vector3(kept.x, 0f, kept.z);
					RestoreBag(doll, kept, catalog);
				}

				dolls[doll.Id] = doll;
				return doll;
			}
		}

		/// <summary>
		/// 이미 들어와 있는 인형에 <b>주인을 붙인다</b> (TASK-WM-218).
		///
		/// ★ 왜 나중에 붙이나: 「인사를 받고 나서 인형을 준다」로 만들었더니, 인사를 안 하는 옛 창이
		///   영영 환영을 못 받고 멈춰 섰다(시험 4개가 그 자리에서 죽었다). 그래서 <b>먼저 받아 주고</b>
		///   열쇠가 오면 그때 기억을 얹는다 — 인사를 안 해도 게임은 돈다.
		/// </summary>
		public bool Adopt(int dollId, int identityId, WorldItemCatalog catalog)
		{
			lock (gate)
			{
				if (identityId == 0 || dolls.TryGetValue(dollId, out WorldDoll doll) == false)
					return false;

				if (doll.IdentityId == identityId)
					return true;

				// ★ 주인은 접속당 한 번만 붙는다 (TASK-WM-218).
				//   안 막으면: A 로 들어와 물건을 줍고 → 도중에 B 열쇠를 내밀어 갈아타고 → 나가면
				//   그 물건이 B 의 것으로 저장된다. 물건이 복제되고 남의 것이 된다.
				if (doll.IdentityId != 0)
					return false;

				doll.IdentityId = identityId;

				if (remembered.TryGetValue(identityId, out PersonSaveData kept))
				{
					doll.Position = new Vector3(kept.x, 0f, kept.z);
					RestoreBag(doll, kept, catalog);
				}

				return true;
			}
		}

		/// <summary>
		/// 그 사람이 세계에 남긴 게 있나 (TASK-WM-218) — 신원 정리 전에 반드시 묻는다.
		/// 지금은 「가방에 뭔가 있거나, 어디엔가 서 있던 자리가 원점이 아니거나」.
		/// ⚠ 나중에 집·밭·친구가 생기면 여기도 같이 늘어야 한다 — 안 그러면 가진 사람이 지워진다.
		/// </summary>
		public bool OwnsSomething(int identityId)
		{
			lock (gate)
			{
				if (remembered.TryGetValue(identityId, out PersonSaveData kept) == false)
					return false;

				if (kept.bag != null && kept.bag.Length > 0)
					return true;

				return kept.x != 0f || kept.z != 0f;
			}
		}

		/// <summary>그 인형의 주인(신원 번호) — 아직 안 붙었으면 0.</summary>
		public int OwnerOf(int dollId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.IdentityId : 0;
			}
		}

		/// <summary>그 사람이 어디에 있었고 뭘 갖고 있었는지 — 나갈 때 여기 적힌다.</summary>
		private readonly Dictionary<int, PersonSaveData> remembered = new Dictionary<int, PersonSaveData>();

		private static void RestoreBag(WorldDoll doll, PersonSaveData kept, WorldItemCatalog catalog)
		{
			if (kept.bag == null || catalog == null)
				return;

			for (int i = 0; i < kept.bag.Length; i++)
			{
				BagSaveEntry entry = kept.bag[i];
				if (entry == null || entry.amount <= 0)
					continue;

				IItemData item = catalog.Find(entry.itemId);
				if (item == null)
					continue; // 세계가 더는 모르는 물건 — 조용히 버린다(가방이 안 열리는 것보다 낫다).

				doll.Bag.Add(item, entry.amount);
			}
		}

		public void Leave(int dollId)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out WorldDoll doll) && doll.IdentityId != 0)
				{
					// 나가도 자리·가방은 세계가 들고 있는다 — 그래야 다시 왔을 때 「내 것」이 있다.
					remembered[doll.IdentityId] = new PersonSaveData
					{
						identityId = doll.IdentityId,
						x = doll.Position.x,
						z = doll.Position.z,
						bag = doll.SaveBag().ToArray(),
					};
				}

				dolls.Remove(dollId);
			}
		}

		/// <summary>지금 접속 중인 사람들 것까지 포함해 뜬다 — 서버가 꺼질 때도 안 잃는다.</summary>
		public PersonSaveData[] SavePeople()
		{
			lock (gate)
			{
				return SavePeopleUnlocked();
			}
		}

		// ⚠ 이미 자물쇠를 쥔 자리에서 부른다 — 여기서 또 lock 하면 안 된다(재진입은 되지만 뜻이 흐려진다).
		private PersonSaveData[] SavePeopleUnlocked()
		{
			{
				Dictionary<int, PersonSaveData> merged = new Dictionary<int, PersonSaveData>(remembered);
				foreach (KeyValuePair<int, WorldDoll> entry in dolls)
				{
					WorldDoll doll = entry.Value;
					if (doll.IdentityId == 0)
						continue;

					merged[doll.IdentityId] = new PersonSaveData
					{
						identityId = doll.IdentityId,
						x = doll.Position.x,
						z = doll.Position.z,
						bag = doll.SaveBag().ToArray(),
					};
				}

				PersonSaveData[] people = new PersonSaveData[merged.Count];
				merged.Values.CopyTo(people, 0);
				return people;
			}
		}

		/// <summary>사람들의 기억을 되살린다.</summary>
		public void LoadPeople(PersonSaveData[] people)
		{
			lock (gate)
			{
				LoadPeopleUnlocked(people);
			}
		}

		private void LoadPeopleUnlocked(PersonSaveData[] people)
		{
			{
				remembered.Clear();
				if (people == null)
					return;

				for (int i = 0; i < people.Length; i++)
				{
					PersonSaveData person = people[i];
					if (person == null || person.identityId == 0)
						continue;

					remembered[person.identityId] = person;
				}
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
				BuildVersion++;
				return true;
			}
		}

		/// <summary>
		/// 놓은 것을 <b>부순다</b> — 그 칸을 물고 있는 건물을 통째로 지운다 (TASK-WM-217).
		/// 모서리를 찍든 가운데를 찍든 같은 건물이 지워진다(사람은 「건물」을 부수지 「칸」을 부수지 않는다).
		/// </summary>
		public bool TryRemoveBuilding(Vector3Int cell)
		{
			lock (gate)
			{
				if (occupiedCells.ContainsKey(cell) == false)
					return false;

				for (int i = 0; i < placed.Count; i++)
				{
					List<Vector3Int> cells = BuildingFootprint.Cells(placed[i].Pivot, placed[i].Size);
					if (cells.Contains(cell) == false)
						continue;

					for (int c = 0; c < cells.Count; c++)
						occupiedCells.Remove(cells[c]);

					placed.RemoveAt(i);
					BuildVersion++;
					return true;
				}

				// 칸은 물려 있는데 주인이 없다 = 장부가 어긋난 것. 그냥 두면 그 칸에 영영 못 짓는다.
				occupiedCells.Remove(cell);
				BuildVersion++;
				return true;
			}
		}

		/// <summary>지어지거나 부서질 때마다 오른다 — 창이 「내 화면이 낡았나」를 이 수로 안다.</summary>
		public int BuildVersion { get; private set; }

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

				return new WorldSaveData
				{
					buildings = saved,
					people = SavePeopleUnlocked(),
					// 시간도 기억한다 — 껐다 켰더니 다시 아침이면 그건 이어진 세계가 아니다.
					year = Calendar.Year,
					season = Calendar.Season,
					day = Calendar.Day,
					hour = Calendar.Hour,
					minute = Calendar.Minute,
				};
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

				if (data == null)
					return 0;

				Calendar.Set(data.year, data.season, data.day, data.hour, data.minute);
				LoadPeopleUnlocked(data.people);

				if (data.buildings == null)
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
					BuildVersion++;
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
