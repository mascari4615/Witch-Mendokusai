using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
	// WorldIdentity.cs 의 Mark 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 표식과 외부 계정으로 찾기.
	public sealed partial class WorldIdentityRegistry
	{
		/// <summary>
		/// 이 사람의 <b>세계 공통 이름표</b> (TASK-WM-259).
		///
		/// ★ 왜 필요한가: 번호(<c>id</c>)는 세계마다 따로 매긴다 — 동쪽의 1번과 서쪽의 1번은 <b>다른 사람</b>이다.
		///   그러니 국경을 건너는 것은 번호일 수 없다. 건너는 것은 이 이름표다:
		///   계정이 있으면 계정(어느 기기에서도 나), 없으면 열쇠의 <b>지문</b>(세계가 달라도 같은 값).
		/// </summary>
		public static string MarkOf(WorldIdentityRecord person)
		{
			if (person == null)
				return string.Empty;

			return string.IsNullOrEmpty(person.externalId) ? person.secretHash : person.externalId;
		}

		/// <summary>그 번호로 아는 사람의 이름표. 모르면 빈 글자.</summary>
		public string MarkOf(int identityId)
		{
			lock (gate)
			{
				return byId.TryGetValue(identityId, out WorldIdentityRecord record) ? MarkOf(record) : string.Empty;
			}
		}

		/// <summary>이 열쇠를 아는 사람 — <b>없으면 안 만든다</b>(있나 없나만 볼 때 쓴다).</summary>
		public WorldIdentityRecord TryFind(string secret)
		{
			if (string.IsNullOrEmpty(secret))
				return null;

			lock (gate)
			{
				return bySecret.TryGetValue(Fingerprint(secret), out WorldIdentityRecord known) ? known : null;
			}
		}

		/// <summary>이 계정으로 아는 사람 — 없으면 안 만든다.</summary>
		public WorldIdentityRecord TryFindExternal(string externalId)
		{
			if (string.IsNullOrEmpty(externalId))
				return null;

			lock (gate)
			{
				foreach (KeyValuePair<int, WorldIdentityRecord> entry in byId)
				{
					if (string.Equals(entry.Value.externalId, externalId, StringComparison.Ordinal))
						return entry.Value;
				}

				return null;
			}
		}

		/// <summary>
		/// 그 이름표로 사는 사람이 <b>여기 있나</b> — 없으면 만들지 않는다 (TASK-WM-377).
		///
		/// ★ 왜 따로 두나: <see cref="RecognizeMark"/> 는 <b>맞이하는</b> 자리라 없으면 만든다.
		///   「옆 세계에 도착했다니 여기서는 내보낸다」처럼 <b>내보내려고</b> 찾을 때 그걸 쓰면
		///   없는 사람을 만들어 놓고 지우는 꼴이 된다(장부에 빈 신원이 쌓인다).
		/// </summary>
		public int FindMark(string mark)
		{
			if (string.IsNullOrEmpty(mark))
				return 0;

			bool account = mark.IndexOf(':') >= 0;

			lock (gate)
			{
				if (account == false)
					return bySecret.TryGetValue(mark, out WorldIdentityRecord known) ? known.id : 0;

				foreach (KeyValuePair<int, WorldIdentityRecord> entry in byId)
				{
					if (string.Equals(entry.Value.externalId, mark, StringComparison.Ordinal))
						return entry.Value.id;
				}

				return 0;
			}
		}

		/// <summary>
		/// 세계 공통 이름표로 사람을 잇는다 (TASK-WM-259) — 처음 보는 이름표면 <b>그 이름표 그대로</b> 만든다.
		///
		/// ★ 왜 그대로 만드나: 세계는 지문만 갖는다. 옆 세계가 도장 찍어 보낸 지문을 그대로 적어 두면,
		///   그 사람이 <b>제 열쇠로</b> 다시 인사할 때 여기서도 알아본다 — 국경을 넘어도 계속 나다.
		/// ⚠ 여기서는 새 열쇠를 주지 않는다. 갈아 주면 창의 열쇠가 바뀌어 <b>돌아갔을 때 남이 된다</b>.
		/// </summary>
		public WorldIdentityRecord RecognizeMark(string mark, string deviceSecret, int today)
		{
			if (string.IsNullOrEmpty(mark))
				return null;

			// 계정 이름표는 「제공자:아이디」 꼴이다 — 지문에는 쌍점이 없다.
			bool account = mark.IndexOf(':') >= 0;

			lock (gate)
			{
				if (account)
				{
					foreach (KeyValuePair<int, WorldIdentityRecord> entry in byId)
					{
						if (string.Equals(entry.Value.externalId, mark, StringComparison.Ordinal) == false)
							continue;

						entry.Value.lastSeenDay = today;
						AttachDevice(deviceSecret, entry.Value);
						return entry.Value;
					}
				}
				else if (bySecret.TryGetValue(mark, out WorldIdentityRecord known))
				{
					known.lastSeenDay = today;
					return known;
				}

				WorldIdentityRecord record = new WorldIdentityRecord
				{
					id = nextId++,
					secretHash = account ? string.Empty : mark,
					externalId = account ? mark : string.Empty,
					lastSeenDay = today,
				};

				byId[record.id] = record;
				if (account)
					AttachDevice(deviceSecret, record);
				else
					bySecret[mark] = record;

				return record;
			}
		}

		/// <summary>계정으로 들어온 사람의 이름을 적어 둔다 — 이미 있으면 덮어쓰지 않는다(사람이 고친 게 이긴다).</summary>
		public void NameIfEmpty(int identityId, string name)
		{
			if (string.IsNullOrWhiteSpace(name))
				return;

			lock (gate)
			{
				if (byId.TryGetValue(identityId, out WorldIdentityRecord record) == false)
					return;

				if (string.IsNullOrWhiteSpace(record.name))
					record.name = name;
			}
		}

		public WorldIdentityRecord RecognizeExternal(string externalId, string deviceSecret, int today, out bool created)
		{
			return RecognizeExternal(externalId, deviceSecret, today, out created, out _);
		}

		/// <summary>계정으로 들어온 사람 — 새로 만들었으면 그때만 평문 열쇠를 준다 (TASK-WM-220).</summary>
		public WorldIdentityRecord RecognizeExternal(string externalId, string deviceSecret, int today,
			out bool created, out string grantedSecret)
		{
			grantedSecret = string.Empty;
			created = false;
			if (string.IsNullOrEmpty(externalId))
				return null;

			lock (gate)
			{
				foreach (KeyValuePair<int, WorldIdentityRecord> entry in byId)
				{
					if (string.Equals(entry.Value.externalId, externalId, StringComparison.Ordinal) == false)
						continue;

					entry.Value.lastSeenDay = today;
					AttachDevice(deviceSecret, entry.Value);
					return entry.Value;
				}

				// 처음 보는 계정 — 그 기기가 이미 손님으로 놀고 있었다면 <b>그 손님을 그대로 승격</b>한다.
				// 새로 만들면 그때까지 모은 게 주인 없이 남는다(사람 눈엔 사라진 것이다).
				if (string.IsNullOrEmpty(deviceSecret) == false && bySecret.TryGetValue(Fingerprint(deviceSecret), out WorldIdentityRecord guest)
					&& string.IsNullOrEmpty(guest.externalId))
				{
					guest.externalId = externalId;
					guest.lastSeenDay = today;
					return guest;
				}

				string freshForAccount = NewSecret();
				grantedSecret = freshForAccount;
				WorldIdentityRecord record = new WorldIdentityRecord
				{
					id = nextId++,
					secretHash = Fingerprint(freshForAccount),
					externalId = externalId,
					lastSeenDay = today,
				};

				bySecret[record.secretHash] = record;
				byId[record.id] = record;
				AttachDevice(deviceSecret, record);
				created = true;
				return record;
			}
		}

		// ⚠ 자물쇠 안에서만 부른다.
		private void AttachDevice(string deviceSecret, WorldIdentityRecord person)
		{
			if (string.IsNullOrEmpty(deviceSecret))
				return;

			bySecret[Fingerprint(deviceSecret)] = person;
		}

		/// <summary>그 번호의 사람 — 모르면 null.</summary>
		public WorldIdentityRecord Find(int id)
		{
			lock (gate)
			{
				return byId.TryGetValue(id, out WorldIdentityRecord record) ? record : null;
			}
		}
	}
}


