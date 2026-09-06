using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
	/// <summary>세계가 아는 사람들 — 저장되는 모양 (TASK-WM-218).</summary>
	[Serializable]
	public class WorldIdentityBook
	{
		public WorldIdentityRecord[] people = Array.Empty<WorldIdentityRecord>();
		public int nextId = 1;

		/// <summary>아직 안 쓴 초대 열쇠들 — 서버가 꺼졌다 켜져도 살아 있어야 쓸모가 있다.</summary>
		public WorldLinkInvite[] invites = Array.Empty<WorldLinkInvite>();
	}
}


