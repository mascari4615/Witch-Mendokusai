using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
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
	public sealed partial class WorldIdentityRegistry
	{
		/// <summary>열쇠 길이(문자) — 짧으면 찍어서 맞힌다.</summary>
		public const int SECRET_LENGTH = 32;

		private const string ALPHABET = "abcdefghijklmnopqrstuvwxyz0123456789";

		private readonly object gate = new object();
		/// <summary>열쇠의 <b>지문</b> → 사람. 열쇠 자체는 세계도 안 갖는다 (TASK-WM-220).</summary>
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
		public WorldIdentityRecord Recognize(string secret, out bool created, int today = 0)
		{
			return Recognize(secret, out created, out _, today);
		}

		/// <summary>
		/// 열쇠로 사람을 찾는다 — 새로 만들었으면 <paramref name="grantedSecret"/> 에 <b>그때만</b>
		/// 평문 열쇠가 담긴다(창에 줘야 하니까). 세계는 그 뒤로 지문만 갖는다 (TASK-WM-220).
		/// </summary>
		public WorldIdentityRecord Recognize(string secret, out bool created, out string grantedSecret, int today = 0)
		{
			lock (gate)
			{
				grantedSecret = string.Empty;

				if (string.IsNullOrEmpty(secret) == false
					&& bySecret.TryGetValue(Fingerprint(secret), out WorldIdentityRecord known))
				{
					known.lastSeenDay = today;
					created = false;
					return known;
				}

				// 모르는 열쇠 = 새 사람. 남의 번호로 이어 주지 않는다.
				string fresh = NewSecret();
				WorldIdentityRecord record = new WorldIdentityRecord
				{
					id = nextId++,
					secretHash = Fingerprint(fresh),
					lastSeenDay = today,
				};

				bySecret[record.secretHash] = record;
				byId[record.id] = record;
				created = true;
				grantedSecret = fresh;
				return record;
			}
		}

		/// <summary>열쇠의 지문 — 세계가 갖는 유일한 형태다.</summary>
		public static string Fingerprint(string secret)
		{
			if (string.IsNullOrEmpty(secret))
				return string.Empty;

			using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
			byte[] digest = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(secret));
			char[] hex = new char[digest.Length * 2];
			for (int i = 0; i < digest.Length; i++)
			{
				hex[i * 2] = "0123456789abcdef"[digest[i] >> 4];
				hex[i * 2 + 1] = "0123456789abcdef"[digest[i] & 0xF];
			}

			return new string(hex);
		}

		/// <summary>
		/// 바깥 계정으로 들어온 사람을 찾는다 (TASK-WM-218 — KarmoLab 계정 공유).
		///
		/// ★ 왜 이 자리가 필요한가: 기기 열쇠는 <b>기기</b>를 알아볼 뿐이다. 사람이 새 기기를 사거나
		///   브라우저를 지우면 남이 된다. 이미 있는 계정(KarmoLab)이 「그 사람」을 알고 있으니,
		///   그 이름표를 신원에 붙이면 <b>어느 기기에서든 나</b>가 된다.
		///
		/// 처음 보는 계정이면 그 자리에서 사람을 만든다(가입 화면은 여전히 없다).
		/// 기기 열쇠를 같이 주면 그 기기도 이 사람 쪽으로 붙는다 — 다음부터는 계정 없이도 나다.
		/// </summary>
		/// <summary>
		/// 그 사람을 뭐라고 부를까 (TASK-WM-218). 계정 이름이 있으면 그것, 없으면 「손님 N」.
		/// ★ 빈칸으로 두면 창마다 다르게 부르게 된다 — 부르는 법도 세계가 정한다.
		/// </summary>
		public string NameOf(int identityId)
		{
			lock (gate)
			{
				if (byId.TryGetValue(identityId, out WorldIdentityRecord record) == false)
					return string.Empty;

				return string.IsNullOrWhiteSpace(record.name) ? "손님 " + record.id : record.name;
			}
		}

		/// <summary>
		/// 사람이 <b>스스로 이름을 정한다</b> (TASK-WM-218).
		///
		/// ★ 왜 세계가 검사하나: 이름은 <b>남에게 보이는 것</b>이다. 창이 정하게 두면 빈 이름·공백만·
		///   끝없이 긴 이름·남과 똑같은 이름이 그대로 세계에 박힌다 — 그러면 「누가 누군지」가 무너진다.
		///   거절할 때는 <b>이유를 준다</b>(조용히 안 바뀌면 사람은 「고장」으로 읽는다).
		/// </summary>
		public bool TryRename(int identityId, string wanted, out string denied)
		{
			denied = null;
			string trimmed = wanted == null ? string.Empty : wanted.Trim();

			if (trimmed.Length < MIN_NAME)
			{
				denied = "이름이 너무 짧다";
				return false;
			}

			if (trimmed.Length > MAX_NAME)
			{
				denied = "이름이 너무 길다";
				return false;
			}

			lock (gate)
			{
				if (byId.TryGetValue(identityId, out WorldIdentityRecord record) == false)
				{
					denied = "세계가 모르는 사람이다";
					return false;
				}

				foreach (WorldIdentityRecord other in byId.Values)
				{
					if (other.id == identityId)
						continue;

					// 같은 이름이 둘이면 「누가 누군지」가 무너진다 — 대소문자만 다른 것도 같은 이름으로 본다.
					if (string.Equals(other.name, trimmed, StringComparison.OrdinalIgnoreCase))
					{
						denied = "이미 그렇게 불리는 사람이 있다";
						return false;
					}
				}

				record.name = trimmed;
				return true;
			}
		}

		/// <summary>이름 길이 — 너무 짧으면 못 알아보고, 너무 길면 남의 화면을 덮는다.</summary>
		public const int MIN_NAME = 1;

		public const int MAX_NAME = 16;
	}
}


