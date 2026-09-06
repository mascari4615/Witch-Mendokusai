using System;
using System.Collections.Generic;

namespace WitchMendokusai.Identity
{
	/// <summary>다른 기기를 같은 사람에 잇기 위한 <b>한 번 쓰는 초대 열쇠</b> (TASK-WM-218).</summary>
	[Serializable]
	public class WorldLinkInvite
	{
		/// <summary>
		/// ⚠ <b>옛 저장 파일을 읽기 위해서만</b> 남아 있는 자리 (TASK-WM-220). 새로 적을 때는 비어 있다.
		/// 초대 열쇠도 그대로 적으면, 파일 한 장으로 <b>남의 사람이 될 수 있다</b>(3일 안이면).
		/// </summary>
		public string code = string.Empty;

		/// <summary>초대 열쇠의 지문 — 세계는 이것만 갖는다.</summary>
		public string codeHash = string.Empty;
		public int identityId;

		/// <summary>이 날(세계 기준 총 일수)이 지나면 못 쓴다 — 주운 종이 한 장이 영원하면 안 된다.</summary>
		public int expiresOnDay;
	}
}


