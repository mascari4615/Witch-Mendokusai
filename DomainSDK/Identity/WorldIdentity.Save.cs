using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
	// WorldIdentity.cs 의 Save 조각. 같은 클래스의 partial. 상태(필드)는 원본 파일을 본다. 저장과 불러오기.
	public sealed partial class WorldIdentityRegistry
	{
		/// <summary>기억을 뜬다.</summary>
		public WorldIdentityBook Save()
		{
			lock (gate)
			{
				WorldIdentityRecord[] people = new WorldIdentityRecord[byId.Count];
				byId.Values.CopyTo(people, 0);
				WorldLinkInvite[] saved = new WorldLinkInvite[invites.Count];
				invites.Values.CopyTo(saved, 0);

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
					if (record == null)
						continue;

					// ★ 옛 파일에는 열쇠가 <b>그대로</b> 적혀 있다 (TASK-WM-220). 읽으면서 지문으로 옮기고
					//   평문은 버린다 — 사람은 쓰던 열쇠를 그대로 쓰고, 파일에서는 사라진다.
					if (string.IsNullOrEmpty(record.secretHash) && string.IsNullOrEmpty(record.secret) == false)
					{
						record.secretHash = Fingerprint(record.secret);
						record.secret = string.Empty;
					}

					if (string.IsNullOrEmpty(record.secretHash))
						continue;

					if (byId.ContainsKey(record.id) || bySecret.ContainsKey(record.secretHash))
						continue;

					byId[record.id] = record;
					bySecret[record.secretHash] = record;
				}

				invites.Clear();
				if (book.invites != null)
				{
					for (int i = 0; i < book.invites.Length; i++)
					{
						WorldLinkInvite invite = book.invites[i];

						// 옛 파일의 평문 코드도 읽으면서 지문으로 옮긴다(사람이 들고 있는 종이는 그대로 통한다).
						if (invite != null && string.IsNullOrEmpty(invite.codeHash) && string.IsNullOrEmpty(invite.code) == false)
						{
							invite.codeHash = Fingerprint(invite.code);
							invite.code = string.Empty;
						}

						if (invite == null || string.IsNullOrEmpty(invite.codeHash) || byId.ContainsKey(invite.identityId) == false)
							continue;

						invites[invite.codeHash] = invite;
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
	}
}


