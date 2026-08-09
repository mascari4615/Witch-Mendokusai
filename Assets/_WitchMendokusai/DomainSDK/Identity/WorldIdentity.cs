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

	/// <summary>세계가 아는 사람들 — 저장되는 모양 (TASK-WM-218).</summary>
	[Serializable]
	public class WorldIdentityBook
	{
		public WorldIdentityRecord[] people = Array.Empty<WorldIdentityRecord>();
		public int nextId = 1;
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
				return new WorldIdentityBook { people = people, nextId = nextId };
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

				nextId = book.nextId > 0 ? book.nextId : 1;

				// 저장된 번호보다 작은 nextId 는 이미 있는 사람을 덮어쓴다 — 그건 남의 것을 주는 짓이다.
				foreach (KeyValuePair<int, WorldIdentityRecord> entry in byId)
				{
					if (entry.Key >= nextId)
						nextId = entry.Key + 1;
				}
			}
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
