using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
	// WorldIdentity.cs 의 Invite 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 초대 코드.
	public sealed partial class WorldIdentityRegistry
	{
		/// <summary>초대 열쇠 길이 — 사람이 손으로 옮겨 적을 수 있어야 해서 짧다.</summary>
		public const int INVITE_LENGTH = 8;

		private readonly Dictionary<string, WorldLinkInvite> invites = new Dictionary<string, WorldLinkInvite>(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// 다른 기기를 <b>같은 사람</b>에 잇기 위한 초대 열쇠를 낸다 (TASK-WM-218).
		/// 짧은 대신 <b>한 번 쓰면 사라진다</b> — 남이 주워도 이미 쓴 것은 소용없다.
		/// </summary>
		/// <summary>초대 열쇠가 살아 있는 날수 — 세계의 하루 기준.</summary>
		public const int INVITE_DAYS = 3;

		public string IssueInvite(int identityId, int today = 0)
		{
			lock (gate)
			{
				if (byId.ContainsKey(identityId) == false)
					return null;

				// 한 사람에게 살아 있는 열쇠는 하나뿐 — 새로 만들면 옛것은 그 자리에서 죽는다.
				// 안 그러면 예전에 만든 종이들이 계속 유효한 채로 굴러다닌다.
				List<string> stale = new List<string>();
				foreach (KeyValuePair<string, WorldLinkInvite> existing in invites)
				{
					if (existing.Value.identityId == identityId)
						stale.Add(existing.Key);
				}

				for (int i = 0; i < stale.Count; i++)
					invites.Remove(stale[i]);

				string code = NewCode(INVITE_LENGTH);
				invites[Fingerprint(code)] = new WorldLinkInvite
				{
					codeHash = Fingerprint(code),
					identityId = identityId,
					expiresOnDay = today + INVITE_DAYS,
				};

				return code;
			}
		}

		/// <summary>
		/// 초대 열쇠를 쓴다 — 그 기기의 열쇠를 <b>같은 사람</b>에 붙인다.
		/// 모르는 코드거나 이미 쓴 코드면 아무 일도 없다(남의 사람이 되지 않는다).
		/// </summary>
		public WorldIdentityRecord RedeemInvite(string code, string deviceSecret, int today = 0)
		{
			return RedeemInvite(code, deviceSecret, today, out int _);
		}

		/// <summary>
		/// 초대 열쇠를 쓴다. <paramref name="previousIdentityId"/> = 그 기기가 <b>전에 쓰던 사람</b> 번호
		/// (0 이면 없음) — 그 사람이 갖고 있던 것을 옮겨 줘야 하는지 부르는 쪽이 판단한다.
		/// </summary>
		public WorldIdentityRecord RedeemInvite(string code, string deviceSecret, int today, out int previousIdentityId)
		{
			previousIdentityId = 0;
			lock (gate)
			{
				if (string.IsNullOrEmpty(deviceSecret) == false && bySecret.TryGetValue(Fingerprint(deviceSecret), out WorldIdentityRecord before))
					previousIdentityId = before.id;

				string codeHash = Fingerprint(code);
				if (string.IsNullOrEmpty(code) || invites.TryGetValue(codeHash, out WorldLinkInvite invite) == false)
					return null;

				if (today > invite.expiresOnDay)
				{
					// 지난 열쇠는 그 자리에서 버린다 — 남아 있으면 나중에 또 시도된다.
					invites.Remove(codeHash);
					return null;
				}

				if (byId.TryGetValue(invite.identityId, out WorldIdentityRecord person) == false)
					return null;

				invites.Remove(codeHash); // 한 번 쓰면 사라진다.

				// ★ 그 기기의 열쇠를 <b>그 사람 쪽으로 옮긴다</b>(이미 딴 사람에 붙어 있어도).
				//   처음엔 「이미 있으면 두기」로 짰는데, 그러면 이어도 아무 일이 안 일어난다 —
				//   기기는 첫 접속에 이미 자기 사람을 갖기 때문이다(시험이 잡았다).
				//   ⚠ 옮기기 전 그 기기의 옛 사람이 갖고 있던 것은 그 사람에게 남는다(합치기는 후속).
				if (string.IsNullOrEmpty(deviceSecret) == false)
					bySecret[Fingerprint(deviceSecret)] = person;

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

		/// <summary>
		/// <b>빈손이고 오래 안 온</b> 사람만 지운다 (TASK-WM-218).
		///
		/// ★ 왜 조심스러운가: 신원을 지우는 건 그 사람의 세계를 지우는 것이다. 그래서 두 조건을
		///   <b>둘 다</b> 만족할 때만 지운다 — ① 세계에 남긴 게 하나도 없다(<paramref name="ownsSomething"/>)
		///   ② 오래 안 왔다. 하나라도 걸리면 남긴다. 「깨끗한 장부」보다 「남의 것을 안 지우는 것」이 위다.
		///
		/// 지운 사람의 초대 열쇠도 같이 버린다(주인 없는 열쇠는 아무 데도 못 간다).
		/// </summary>
		public int PruneGuests(int today, int notSeenForDays, Func<int, bool> ownsSomething)
		{
			lock (gate)
			{
				List<WorldIdentityRecord> doomed = new List<WorldIdentityRecord>();
				foreach (KeyValuePair<int, WorldIdentityRecord> entry in byId)
				{
					WorldIdentityRecord person = entry.Value;
					if (today - person.lastSeenDay < notSeenForDays)
						continue;

					if (ownsSomething != null && ownsSomething(person.id))
						continue;

					// ★ <b>이름을 지은 사람은 안 지운다</b> (TASK-WM-363).
					//   이름은 「나 여기 산다」는 표시다 — 가방이 비었고 아직 한 걸음도 안 걸었어도,
					//   그 이름으로 남들이 그를 부른다(말풍선·이름표에 남아 있다). 지우면 다음에 왔을 때
					//   <b>남이 된다</b>(열쇠가 안 통하고 이름도 없다). 장부가 조금 커지는 것보다 그게 나쁘다.
					if (string.IsNullOrEmpty(person.name) == false)
						continue;

					doomed.Add(person);
				}

				for (int i = 0; i < doomed.Count; i++)
				{
					byId.Remove(doomed[i].id);
					bySecret.Remove(doomed[i].secret);

					List<string> orphanCodes = new List<string>();
					foreach (KeyValuePair<string, WorldLinkInvite> invite in invites)
					{
						if (invite.Value.identityId == doomed[i].id)
							orphanCodes.Add(invite.Key);
					}

					for (int c = 0; c < orphanCodes.Count; c++)
						invites.Remove(orphanCodes[c]);
				}

				return doomed.Count;
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
			return bySecret.ContainsKey(Fingerprint(secret)) ? NewSecret() : secret;
		}
	}
}


