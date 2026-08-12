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

		/// <summary>솥 건물의 번호 — 이걸 지으면 그 자리에 솥이 하나 생긴다 (TASK-WM-217).</summary>
		public const int CAULDRON_BUILDING_ID = 4000;

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

		/// <summary>
		/// ⚠ <b>폐기</b> — 세계에 하나뿐이던 솥 (TASK-WM-217). 지은 자리마다의 <see cref="Cauldrons"/> 로 옮겼다.
		/// 규칙이 두 벌이면 「내 솥에 넣었는데 남의 화면에선 딴 솥이 움직이는」 일이 생긴다.
		/// 아직 지우지 않은 이유는 하나뿐: 옛 시험이 이걸 부른다(그 시험이 옮겨지면 지운다).
		/// </summary>
		public WorldCauldron Cauldron { get; } = new WorldCauldron();

		/// <summary>
		/// 세계에 흩어져 있는 주울 것 (TASK-WM-217). 서버가 「무엇이 자라는 세계인가」를 정해 꽂아 준다.
		/// 안 꽂으면 빈 들판이다 — 아무것도 안 자라는 세계에서는 아무도 못 줍는다(우겨도).
		/// </summary>
		public WorldGatherables Gatherables { get; set; } = new WorldGatherables(null);

		/// <summary>
		/// 세계가 아는 건물 목록 (TASK-WM-217) — 「그건 몇 칸짜리인가」의 정본.
		/// 안 꽂으면 아무것도 못 짓는다(세계가 모르는 것은 서지 않는다).
		/// </summary>
		public WorldBuildingCatalog Buildables { get; set; } = new WorldBuildingCatalog(null);

		/// <summary>
		/// 솥에 넣을 수 있는 재료들 (TASK-WM-217) — 「무엇을 넣으면 어디로 가나」의 정본.
		/// 안 꽂으면 아무것도 못 넣는다(창이 방향을 우기던 길을 대신한다).
		/// </summary>
		public WorldIngredients Ingredients { get; set; } = new WorldIngredients(null);

		/// <summary>
		/// 세계에 놓인 상자들 (TASK-WM-217 후속) — 내가 넣고 친구가 꺼낸다.
		/// 상자인지·몇 칸인지는 건물 목록이 정한다.
		/// </summary>
		public WorldStorages Storages { get; } = new WorldStorages();

		/// <summary>
		/// 지은 자리마다의 솥 (TASK-WM-217) — 여럿이 <b>동시에</b> 조리하려면 솥도 여럿이어야 한다.
		/// 세계에 하나뿐인 <see cref="Cauldron"/> 은 옛 경로로 남는다(아직 그걸 쓰는 창이 있다).
		/// </summary>
		public WorldCauldrons Cauldrons { get; } = new WorldCauldrons();

		/// <summary>시간을 흘린다. 하루가 바뀌었으면 true.</summary>
		public bool AdvanceMinutes(float minutes)
		{
			bool moved;
			lock (gate)
			{
				moved = Calendar.AdvanceMinutes(minutes);
			}

			// ★ 시간이 흐르면 들판도 자란다 (TASK-WM-217). 전에는 재생이 「들판을 훑을 때」만 일어났고,
			//   훑는 쪽은 「바뀌었을 때만」 훑었다 — 서로를 기다리다 다시 자란 것이 창에 안 돌아왔다.
			Gatherables?.Tick(Calendar.TotalMinutes());
			return moved;
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
		public bool Adopt(int dollId, int identityId, WorldItemCatalog catalog) => Adopt(dollId, identityId, catalog, out int _);

		/// <summary>
		/// 주인을 붙인다. 그 사람이 <b>이미 다른 창으로 들어와 있으면 그 창을 내보낸다</b>
		/// (<paramref name="evictedDollId"/> 로 알려 준다) — 일반 MMORPG 의 중복 로그인 규칙이다.
		///
		/// ★ 왜 내보내나: 창 둘이 각자 인형을 가지면 나갈 때 서로의 자리·가방을 덮어쓴다
		///   (마지막에 나간 쪽이 이긴다 = 조용한 데이터 유실). 새로 온 쪽을 살리는 이유는
		///   「지금 손에 쥔 사람」이 진짜 그 사람일 확률이 높아서다(끊긴 옛 연결이 남아 있는 경우 포함).
		/// </summary>
		public bool Adopt(int dollId, int identityId, WorldItemCatalog catalog, out int evictedDollId)
		{
			evictedDollId = 0;
			lock (gate)
			{
				if (identityId != 0)
				{
					foreach (KeyValuePair<int, WorldDoll> existing in dolls)
					{
						if (existing.Key != dollId && existing.Value.IdentityId == identityId)
						{
							evictedDollId = existing.Key;
							break;
						}
					}

					if (evictedDollId != 0)
						RememberAndRemove(evictedDollId); // 내보내기 전에 그 창의 것을 적어 둔다.
				}

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

		/// <summary>그 인형이 지금 서 있는 자리 — 없는 인형이면 원점.</summary>
		public Vector3 PositionOf(int dollId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.Position : Vector3.zero;
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

		/// <summary>
		/// 옆 세계에서 <b>걸어 들어온 사람</b>을 세운다 (TASK-WM-254).
		/// 통행증에 적힌 신원·자리·가방을 그대로 얹는다 — 도장은 이미 확인된 뒤에 부른다.
		/// </summary>
		public void WelcomeTraveller(int dollId, int identityId, Vector3 spot,
			IReadOnlyList<(int ItemId, int Amount)> carried, WorldItemCatalog catalog, int health)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out WorldDoll doll) == false)
					return;

				doll.IdentityId = identityId;

				// ⚠ 몸도 들고 온 그대로다 (TASK-WM-258) — 안 그러면 <b>국경이 회복 장소</b>가 된다
				//   (맞기 직전에 넘어갔다 오면 가득 찬다). 0 이하로 온 것은 안 받는다(쓰러진 채 걸어올 순 없다).
				if (health > 0)
					doll.Health = health < Net.StrikeRule.FULL_HEALTH ? health : Net.StrikeRule.FULL_HEALTH;

				// 들어온 자리가 내 땅이 아니면 경계로 당긴다 — 남의 땅에 세우면 두 세계가 갈라진다.
				doll.Position = Patch.Clamp(spot);

				if (carried == null || catalog == null)
					return;

				// ⚠ 통행증이 <b>진실</b>이다 — 이 세계가 들고 있던 옛 가방 위에 얹으면 오가며 불어난다
				//   (TASK-WM-259: 국경을 넘을 때 보낸 세계가 기억을 지우지만, 낡은 저장분이 남아 있을 수 있다).
				doll.EmptyBag();
				foreach ((int ItemId, int Amount) held in carried)
				{
					if (held.Amount <= 0)
						continue;

					IItemData item = catalog.Find(held.ItemId);
					if (item == null)
						continue; // 저 세계에만 있던 물건 — 조용히 버린다(가방이 안 열리는 것보다 낫다).

					doll.Bag.Add(item, held.Amount);
				}
			}
		}

		/// <summary>
		/// 이 사람이 이 세계에 두고 간 것을 <b>지운다</b> (TASK-WM-259).
		///
		/// ★ 왜: 국경을 넘을 때 자리·가방은 통행증에 실려 <b>같이 간다</b>. 그런데 나가면서 세계가
		///   「다시 오면 줄 것」으로 한 벌 더 기억해 두면, 돌아왔을 때 <b>두 벌</b>이 된다(복사).
		/// </summary>
		public void ForgetPerson(int identityId)
		{
			if (identityId == 0)
				return;

			lock (gate)
			{
				remembered.Remove(identityId);
			}
		}

		public void Leave(int dollId)
		{
			lock (gate)
			{
				RememberAndRemove(dollId);
			}
		}

		// ⚠ 이미 자물쇠를 쥔 자리에서 부른다.
		private void RememberAndRemove(int dollId)
		{
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

		/// <summary>
		/// 한 사람이 갖고 있던 것을 <b>다른 사람에게 옮긴다</b> (TASK-WM-218 — 기기를 이을 때).
		///
		/// ★ 왜 필요한가: 컴퓨터에서 잠깐 놀다(손님 신원으로 몇 개 주움) 폰의 열쇠로 이으면,
		///   그 손님이 갖고 있던 것이 <b>주인 없는 채로 남는다</b> — 사람 눈엔 그냥 사라진 것이다.
		///
		/// 가방은 합치고(넘치면 남는 건 버려진다 — 가방 규칙은 그대로), 자리는 <b>받는 쪽</b>을 지킨다.
		/// 옮긴 뒤 옛 사람의 기록은 지운다(둘 다 남으면 다음 접속에 어느 쪽이 나올지 알 수 없다).
		/// </summary>
		public bool MergePerson(int fromIdentityId, int intoIdentityId, WorldItemCatalog catalog)
		{
			if (fromIdentityId == 0 || intoIdentityId == 0 || fromIdentityId == intoIdentityId)
				return false;

			lock (gate)
			{
				if (remembered.TryGetValue(fromIdentityId, out PersonSaveData from) == false)
					return false;

				if (remembered.TryGetValue(intoIdentityId, out PersonSaveData into) == false)
				{
					// 받는 쪽 기록이 아직 없으면 그대로 옮겨 준다(자리도 같이 간다).
					from.identityId = intoIdentityId;
					remembered[intoIdentityId] = from;
					remembered.Remove(fromIdentityId);
					return true;
				}

				Dictionary<int, int> merged = new Dictionary<int, int>();
				AddBagInto(merged, into.bag);
				AddBagInto(merged, from.bag);

				List<BagSaveEntry> bag = new List<BagSaveEntry>();
				foreach (KeyValuePair<int, int> entry in merged)
					bag.Add(new BagSaveEntry { itemId = entry.Key, amount = entry.Value });

				into.bag = bag.ToArray();
				remembered.Remove(fromIdentityId);
				return true;
			}
		}

		private static void AddBagInto(Dictionary<int, int> target, BagSaveEntry[] source)
		{
			if (source == null)
				return;

			for (int i = 0; i < source.Length; i++)
			{
				BagSaveEntry entry = source[i];
				if (entry == null || entry.amount <= 0)
					continue;

				target.TryGetValue(entry.itemId, out int had);
				target[entry.itemId] = had + entry.amount;
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
		/// <summary>
		/// 짓는다 — <b>크기는 세계가 정한다</b> (TASK-WM-217).
		///
		/// ★ 왜: 전에는 창이 크기를 같이 보냈고 세계는 그대로 믿었다. 그러면 창을 고친 사람이
		///   「이건 1×1 이다」라고 우기며 남의 집에 겹쳐 지을 수 있고, 게임 창과 웹 창이 같은 집을
		///   다른 크기로 그린다. 세계가 모르는 건물은 아예 서지 않는다.
		/// </summary>
		public bool TryPlaceBuilding(Vector3Int pivot, int buildingId, WorldBuildingCatalog catalog)
		{
			if (catalog == null || catalog.TrySize(buildingId, out int width, out int length) == false)
				return false;

			if (TryPlaceBuilding(pivot, new Vector2Int(width, length), buildingId) == false)
				return false;

			// 상자면 그 자리에 빈 상자를 놓는다 — 지은 것이 쓸모를 갖는 자리다.
			Storages.Place(pivot, catalog.SlotsOf(buildingId));

			// 솥이면 그 자리에 빈 솥을 놓는다 — 지은 사람이 자기 솥에서 젓는다.
			if (buildingId == CAULDRON_BUILDING_ID)
				Cauldrons.Place(pivot);

			return true;
		}

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
		public bool TryRemoveBuilding(Vector3Int cell) => TryRemoveBuilding(cell, out int _);

		/// <summary>
		/// 부순다 — <b>무엇이었는지</b>도 알려 준다 (TASK-WM-217).
		/// 재료를 얼마쯤 돌려주려면 부르는 쪽이 「그게 뭐였나」를 알아야 한다.
		/// </summary>
		public bool TryRemoveBuilding(Vector3Int cell, out int removedBuildingId)
		{
			removedBuildingId = 0;
			lock (gate)
			{
				if (occupiedCells.ContainsKey(cell) == false)
					return false;

				// 상자였으면 상자도 같이 사라진다(안에 든 것도) — 창이 사람에게 먼저 물어야 한다.
				Storages.Remove(cell);
				Cauldrons.Remove(cell);

				for (int i = 0; i < placed.Count; i++)
				{
					List<Vector3Int> cells = BuildingFootprint.Cells(placed[i].Pivot, placed[i].Size);
					if (cells.Contains(cell) == false)
						continue;

					for (int c = 0; c < cells.Count; c++)
						occupiedCells.Remove(cells[c]);

					removedBuildingId = placed[i].BuildingId;
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

		/// <summary>그 인형이 이만큼을 받을 자리가 있나 — 되돌릴 수 없는 보상 전에 묻는다.</summary>
		public bool CanReceive(int dollId, IItemData itemData, int amount)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) && doll.Bag.CanReceive(itemData, amount);
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

		/// <summary>
		/// 그 인형의 가방 전부 (TASK-WM-217 — 창이 진짜 가방을 보이려면 필요하다).
		/// ⚠ 전에는 서버가 <b>아는 아이템 두 종류만</b> 물어 봤다 — 나머지는 가방에 있어도 창에 안 보였다.
		/// </summary>
		/// <summary>그 사람의 몸 — 없으면 0 (TASK-WM-258).</summary>
		public int HealthOf(int dollId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.Health : 0;
			}
		}

		public List<BagSaveEntry> BagOf(int dollId)
		{
			lock (gate)
			{
				return dolls.TryGetValue(dollId, out WorldDoll doll) ? doll.SaveBag() : new List<BagSaveEntry>();
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
					gathered = Gatherables.Save().ToArray(),
					storages = Storages.Save().ToArray(),
					cauldrons = Cauldrons.Save().ToArray(),
				};
			}
		}

		/// <summary>
		/// 기억을 되살린다 (TASK-WM-217 단계 5). <b>지금 있는 건물은 지우고</b> 저장된 것으로 갈아끼운다.
		///
		/// 겹치는 건물은 <b>버린다</b> — 저장 파일이 망가졌거나 규칙이 바뀌었을 때
		/// 겹친 채로 되살리면 그 뒤로 짓기 판정이 영원히 이상해진다. 되살린 개수를 돌려준다.
		/// </summary>
		public int Load(WorldSaveData data) => Load(data, null);

		/// <summary>
		/// 기억을 되살린다. <paramref name="catalog"/> 는 상자 안의 물건을 알아보는 데 쓴다 —
		/// 없으면 상자는 서되 <b>안은 빈다</b>(모르는 물건을 지어내지 않는다).
		/// </summary>
		public int Load(WorldSaveData data, WorldItemCatalog catalog)
		{
			lock (gate)
			{
				placed.Clear();
				occupiedCells.Clear();

				if (data == null)
					return 0;

				Calendar.Set(data.year, data.season, data.day, data.hour, data.minute);
				LoadPeopleUnlocked(data.people);
				Gatherables.Load(data.gathered);

				// 상자는 건물을 되살린 <b>뒤에</b> 채운다 — 어느 자리가 몇 칸인지 건물이 정하기 때문이다.
				StorageSaveEntry[] storagesToRestore = data.storages;

				if (data.buildings == null)
				{
					RestoreStoragesUnlocked(storagesToRestore, catalog);
					return 0;
				}

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

				// 상자는 건물을 다 세운 뒤에 채운다 — 어느 자리가 몇 칸인지 건물이 정하기 때문이다.
				RestoreStoragesUnlocked(storagesToRestore, catalog);

				// 솥도 같이 되살린다 — 안 하면 지은 솥이 남아 있는데 못 젓는 세계가 된다.
				Cauldrons.Load(data.cauldrons);
				return restored;
			}
		}

		/// <summary>되살린 건물 위에 상자를 얹는다 — 그 자리에 선 건물이 상자가 아니면 버린다.</summary>
		private void RestoreStoragesUnlocked(StorageSaveEntry[] saved, WorldItemCatalog catalog)
		{
			Storages.Load(saved, cell =>
			{
				for (int i = 0; i < placed.Count; i++)
				{
					if (placed[i].Pivot.Equals(cell))
						return Buildables.SlotsOf(placed[i].BuildingId);
				}

				return 0;
			}, catalog);
		}

		/// <summary>
		/// 때린다 (TASK-WM-251) — 판정은 <see cref="Net.StrikeRule"/> 이 한다.
		/// 되는 경우에만 몸이 줄고, 0 이 되면 그 사람은 <b>다시 세워진다</b>(원점·가득 찬 몸).
		/// </summary>
		public Net.StrikeRule.Denial TryStrike(int attackerId, int targetId, long nowMs,
			out int healthLeft, out bool wentDown)
		{
			healthLeft = 0;
			wentDown = false;

			lock (gate)
			{
				if (dolls.TryGetValue(attackerId, out WorldDoll attacker) == false)
					return Net.StrikeRule.Denial.NoSuchOne;

				bool targetExists = dolls.TryGetValue(targetId, out WorldDoll target);
				Vector3 to = targetExists ? target.Position : Vector3.zero;
				int health = targetExists ? target.Health : 0;

				Net.StrikeRule.Denial why = Net.StrikeRule.CanStrike(attackerId, targetId, targetExists,
					attacker.Position, to, health, attacker.LastStruckMs, nowMs);
				if (why != Net.StrikeRule.Denial.None)
					return why;

				attacker.LastStruckMs = nowMs;
				target.Health = Net.StrikeRule.HealthAfterHit(target.Health);
				healthLeft = target.Health;

				if (target.Health <= 0)
				{
					// ⚠ 쓰러진 채로 두면 그 사람은 <b>게임에서 나간 것</b>이 된다. 뼈대에서는 곧바로 세운다 —
					//   어떻게 되살아날지(자리·기다림·잃는 것)는 게임이 정할 몫이다.
					target.Health = Net.StrikeRule.FULL_HEALTH;
					target.Position = Vector3.zero;
					healthLeft = target.Health;
					wentDown = true;
				}

				return Net.StrikeRule.Denial.None;
			}
		}

		/// <summary>
		/// 이 세계가 <b>맡은 땅</b> (TASK-WM-252). 기본은 온 세상 — 안 나눈 세계는 그대로 돈다.
		/// 나뉜 세계에서는 사람이 이 밖으로 못 나간다: 남의 땅을 내가 굴리면 두 세계가 갈라진다.
		/// </summary>
		public Net.ZonePatch Patch { get; set; } = Net.ZonePatch.Everywhere;

		public bool TryMove(int dollId, Vector3 delta)
		{
			lock (gate)
			{
				if (dolls.TryGetValue(dollId, out WorldDoll doll) == false)
					return false;

				Vector3 clamped = Vector3.ClampMagnitude(delta, MAX_STEP);
				doll.Position = Patch.Clamp(doll.Position + clamped);
				return true;
			}
		}
	}
}
