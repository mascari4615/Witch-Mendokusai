using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
	/// <summary>
	/// 세계가 아는 <b>사람 하나</b> (TASK-WM-218).
	///
	/// 이름도 얼굴도 아직 없다 — 「다시 온 그 사람」을 알아보는 데 필요한 최소만 있다.
	/// <see cref="Secret"/> 는 <b>세계만</b> 갖는다(창에는 그것만 주고, 세계는 그걸로 알아본다).
	/// </summary>
	[Serializable]
	public class WorldIdentityRecord
	{
		/// <summary>세계 안에서의 번호 — 인형·가방·집이 이 번호에 붙는다.</summary>
		public int id;

		/// <summary>창이 갖고 다니는 열쇠. 이게 맞아야 그 사람이다.</summary>
		public string secret = string.Empty;

		/// <summary>마지막으로 본 시각(세계 기준 총 일수) — 오래된 신원 정리에 쓸 수 있다.</summary>
		public int lastSeenDay;
	}

	/// <summary>다른 기기를 같은 사람에 잇기 위한 <b>한 번 쓰는 초대 열쇠</b> (TASK-WM-218).</summary>
	[Serializable]
	public class WorldLinkInvite
	{
		public string code = string.Empty;
		public int identityId;
	}

	/// <summary>세계가 아는 사람들 — 저장되는 모양 (TASK-WM-218).</summary>
	[Serializable]
	public class WorldIdentityBook
	{
		public WorldIdentityRecord[] people = Array.Empty<WorldIdentityRecord>();
		public int nextId = 1;

		/// <summary>아직 안 쓴 초대 열쇠들 — 서버가 꺼졌다 켜져도 살아 있어야 쓸모가 있다.</summary>
		public WorldLinkInvite[] invites = Array.Empty<WorldLinkInvite>();
	}

	/// <summary>
	/// 「다시 온 그 사람인가」를 판정하는 자리 (TASK-WM-218).
	///
	/// ★ 왜 판정 층인가: 이 규칙이 틀리면 <b>남의 가방이 열린다</b>. 눈으로는 절대 못 보는 종류의
	///   사고라, 엔진 밖에서 시험할 수 있는 자리에 둔다. 서버·유니티·웹이 같은 규칙을 쓴다.
	///
	/// 규칙:
	/// <list type="bullet">
	/// <item>열쇠가 맞으면 그 사람 — 늘 같은 번호를 돌려준다</item>
	/// <item>열쇠가 없거나 모르는 것이면 <b>새 사람</b> — 남의 것을 절대 주지 않는다</item>
	/// <item>열쇠는 세계가 만든다(창이 정하지 못한다 — 정하게 두면 남의 번호를 부를 수 있다)</item>
	/// </list>
	/// </summary>
	public sealed class WorldIdentityRegistry
	{
		/// <summary>열쇠 길이(문자) — 짧으면 찍어서 맞힌다.</summary>
		public const int SECRET_LENGTH = 32;

		private const string ALPHABET = "abcdefghijklmnopqrstuvwxyz0123456789";

		private readonly object gate = new object();
		private readonly Dictionary<string, WorldIdentityRecord> bySecret = new Dictionary<string, WorldIdentityRecord>(StringComparer.Ordinal);
		private readonly Dictionary<int, WorldIdentityRecord> byId = new Dictionary<int, WorldIdentityRecord>();
		private readonly Random random;
		private int nextId = 1;

		public WorldIdentityRegistry() : this(new Random())
		{
		}

		/// <summary>주사위를 밖에서 넣는다 — 시험이 같은 답을 얻을 수 있게.</summary>
		public WorldIdentityRegistry(Random dice)
		{
			random = dice ?? new Random();
		}

		/// <summary>세계가 아는 사람 수.</summary>
		public int Count
		{
			get
			{
				lock (gate)
				{
					return byId.Count;
				}
			}
		}

		/// <summary>
		/// 창이 내민 열쇠로 사람을 찾는다. 맞으면 그 사람, 아니면 <b>새 사람</b>을 만들어 준다.
		/// <paramref name="created"/> 가 true 면 창에 새 열쇠를 줘야 한다(기기에 저장하도록).
		/// </summary>
		public WorldIdentityRecord Recognize(string secret, out bool created)
		{
			lock (gate)
			{
				if (string.IsNullOrEmpty(secret) == false && bySecret.TryGetValue(secret, out WorldIdentityRecord known))
				{
					created = false;
					return known;
				}

				// 모르는 열쇠 = 새 사람. 남의 번호로 이어 주지 않는다.
				WorldIdentityRecord record = new WorldIdentityRecord
				{
					id = nextId++,
					secret = NewSecret(),
				};

				bySecret[record.secret] = record;
				byId[record.id] = record;
				created = true;
				return record;
			}
		}

		/// <summary>그 번호의 사람 — 모르면 null.</summary>
		public WorldIdentityRecord Find(int id)
		{
			lock (gate)
			{
				return byId.TryGetValue(id, out WorldIdentityRecord record) ? record : null;
			}
		}

		/// <summary>기억을 뜬다.</summary>
		public WorldIdentityBook Save()
		{
			lock (gate)
			{
				WorldIdentityRecord[] people = new WorldIdentityRecord[byId.Count];
				byId.Values.CopyTo(people, 0);
				WorldLinkInvite[] saved = new WorldLinkInvite[invites.Count];
				int index = 0;
				foreach (KeyValuePair<string, int> invite in invites)
					saved[index++] = new WorldLinkInvite { code = invite.Key, identityId = invite.Value };

				return new WorldIdentityBook { people = people, nextId = nextId, invites = saved };
			}
		}

		/// <summary>기억을 되살린다. 망가진 줄(열쇠 없음·번호 겹침)은 버린다 — 세계가 안 열리는 것보다 낫다.</summary>
		public void Load(WorldIdentityBook book)
		{
			lock (gate)
			{
				bySecret.Clear();
				byId.Clear();
				nextId = 1;

				if (book?.people == null)
					return;

				for (int i = 0; i < book.people.Length; i++)
				{
					WorldIdentityRecord record = book.people[i];
					if (record == null || string.IsNullOrEmpty(record.secret))
						continue;

					if (byId.ContainsKey(record.id) || bySecret.ContainsKey(record.secret))
						continue;

					byId[record.id] = record;
					bySecret[record.secret] = record;
				}

				invites.Clear();
				if (book.invites != null)
				{
					for (int i = 0; i < book.invites.Length; i++)
					{
						WorldLinkInvite invite = book.invites[i];
						if (invite == null || string.IsNullOrEmpty(invite.code) || byId.ContainsKey(invite.identityId) == false)
							continue;

						invites[invite.code] = invite.identityId;
					}
				}

				nextId = book.nextId > 0 ? book.nextId : 1;

				// 저장된 번호보다 작은 nextId 는 이미 있는 사람을 덮어쓴다 — 그건 남의 것을 주는 짓이다.
				foreach (KeyValuePair<int, WorldIdentityRecord> entry in byId)
				{
					if (entry.Key >= nextId)
						nextId = entry.Key + 1;
				}
			}
		}

		/// <summary>초대 열쇠 길이 — 사람이 손으로 옮겨 적을 수 있어야 해서 짧다.</summary>
		public const int INVITE_LENGTH = 8;

		private readonly Dictionary<string, int> invites = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// 다른 기기를 <b>같은 사람</b>에 잇기 위한 초대 열쇠를 낸다 (TASK-WM-218).
		/// 짧은 대신 <b>한 번 쓰면 사라진다</b> — 남이 주워도 이미 쓴 것은 소용없다.
		/// </summary>
		public string IssueInvite(int identityId)
		{
			lock (gate)
			{
				if (byId.ContainsKey(identityId) == false)
					return null;

				string code = NewCode(INVITE_LENGTH);
				invites[code] = identityId;
				return code;
			}
		}

		/// <summary>
		/// 초대 열쇠를 쓴다 — 그 기기의 열쇠를 <b>같은 사람</b>에 붙인다.
		/// 모르는 코드거나 이미 쓴 코드면 아무 일도 없다(남의 사람이 되지 않는다).
		/// </summary>
		public WorldIdentityRecord RedeemInvite(string code, string deviceSecret)
		{
			lock (gate)
			{
				if (string.IsNullOrEmpty(code) || invites.TryGetValue(code, out int identityId) == false)
					return null;

				if (byId.TryGetValue(identityId, out WorldIdentityRecord person) == false)
					return null;

				invites.Remove(code); // 한 번 쓰면 사라진다.

				// ★ 그 기기의 열쇠를 <b>그 사람 쪽으로 옮긴다</b>(이미 딴 사람에 붙어 있어도).
				//   처음엔 「이미 있으면 두기」로 짰는데, 그러면 이어도 아무 일이 안 일어난다 —
				//   기기는 첫 접속에 이미 자기 사람을 갖기 때문이다(시험이 잡았다).
				//   ⚠ 옮기기 전 그 기기의 옛 사람이 갖고 있던 것은 그 사람에게 남는다(합치기는 후속).
				if (string.IsNullOrEmpty(deviceSecret) == false)
					bySecret[deviceSecret] = person;

				return person;
			}
		}

		/// <summary>아직 안 쓴 초대 열쇠 수 — 시험·점검용.</summary>
		public int PendingInvites
		{
			get
			{
				lock (gate)
				{
					return invites.Count;
				}
			}
		}

		private string NewCode(int length)
		{
			char[] buffer = new char[length];
			for (int i = 0; i < buffer.Length; i++)
				buffer[i] = ALPHABET[random.Next(ALPHABET.Length)];

			string code = new string(buffer);
			return invites.ContainsKey(code) ? NewCode(length) : code;
		}

		private string NewSecret()
		{
			char[] buffer = new char[SECRET_LENGTH];
			for (int i = 0; i < buffer.Length; i++)
				buffer[i] = ALPHABET[random.Next(ALPHABET.Length)];

			string secret = new string(buffer);

			// 억지로 겹칠 확률은 사실상 0 이지만, 겹치면 조용히 남의 것이 된다 — 그래서 확인한다.
			return bySecret.ContainsKey(secret) ? NewSecret() : secret;
		}
	}
}
