using System.Collections.Generic;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// WorldSim.cs 의 People 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 사람(인형)의 입장, 퇴장, 기억.
	public sealed partial class WorldSim
	{
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
	}
}


