using System;

namespace WitchMendokusai.Net
{
	/// <summary>클라이언트가 세계에 들어오며 자신의 기기 열쇠를 제출하는 메시지.</summary>
	[Serializable]
	public class HelloMessage
	{
		public string type = NetMessageType.HELLO;
		public string secret = string.Empty;

		/// <summary>
		/// KarmoLab 에서 받은 <b>연결 코드</b>(있으면) — 세션 쿠키를 못 읽는 창(게임)용 길이다.
		/// 초대 열쇠와 같은 모양이라 사람이 이미 아는 손짓이다.
		/// </summary>
		public string klCode = string.Empty;

		/// <summary>
		/// KarmoLab 로그인 세션(있으면) — 이게 있으면 <b>어느 기기에서든 나</b>다 (TASK-WM-218).
		/// 없어도 된다: 그때는 기기 열쇠만으로 손님처럼 논다.
		/// </summary>
		public string klSession = string.Empty;
	}

	/// <summary>
	/// 서버 → 창: 접속했다, 네 인형 번호는 이것이다.
	/// <see cref="secret"/> 가 비어 있지 않으면 <b>새로 받은 열쇠</b>다 — 기기에 적어 둬야 다음에 「나」다.
	/// </summary>
	[Serializable]
	public class WelcomeMessage
	{
		public string type = NetMessageType.WELCOME;
		public int id;
		public string secret = string.Empty;
		public int identityId;
	}

	/// <summary>서버 → 그 창에게만: 네 인형은 여기 있다 (몰린 칸에서 공유 소식에 자기가 빠졌을 때).</summary>
	[Serializable]
	public class MeMessage
	{
		public string type = NetMessageType.ME;
		public WorldDollView doll;
	}

	/// <summary>서버 → 창: 이름표(바뀐 사람만). 창은 이걸 들고 있다가 인형에 붙인다.</summary>
	[Serializable]
	public class NamesMessage
	{
		public string type = NetMessageType.NAMES;
		public DollNameView[] dolls = Array.Empty<DollNameView>();
	}

	/// <summary>누가 무슨 이름인가 — 바뀔 때만 온다.</summary>
	[Serializable]
	public class DollNameView
	{
		public int id;
		public string name = string.Empty;
	}

	/// <summary>창 → 서버: 초대 열쇠를 만들어 줘.</summary>
	[Serializable]
	public class InviteAskMessage
	{
		public string type = NetMessageType.INVITE_ASK;
	}

	/// <summary>서버 → 그 창에게만: 초대 열쇠(한 번만 쓴다).</summary>
	[Serializable]
	public class InviteMessage
	{
		public string type = NetMessageType.INVITE;
		public string code = string.Empty;
	}

	/// <summary>창 → 서버: 이 초대 열쇠로 나를 그 사람에 이어 줘.</summary>
	[Serializable]
	public class LinkMessage
	{
		public string type = NetMessageType.LINK;
		public string code = string.Empty;
	}

	/// <summary>
	/// 서버 → 그 창에게만: 이었나 (TASK-WM-218).
	/// 이었으면 <b>다시 들어와야</b> 그 사람의 인형으로 논다 — 접속 도중 주인 갈아타기는 막혀 있다.
	/// </summary>
	[Serializable]
	public class LinkedMessage
	{
		public string type = NetMessageType.LINKED;
		public bool ok;
		public int identityId;
	}

	/// <summary>
	/// 서버 → 그 창에게만: <b>다른 곳에서 같은 사람이 들어왔다</b> (TASK-WM-218).
	/// 일반 MMORPG 의 중복 로그인 규칙 — 나중에 온 쪽이 이긴다. 여기까지 온 창은 조용히 나간다.
	/// </summary>
	[Serializable]
	public class KickedMessage
	{
		public string type = NetMessageType.KICKED;
		public string reason = "다른 곳에서 접속했다";
	}

	/// <summary>
	/// 서버 → 그 창에게만: <b>이 세계는 지금 가득 찼다</b> (TASK-WM-349).
	///
	/// ★ 왜 마디로 말하나: 문 앞에서 말없이 끊으면 창에는 「연결이 안 된다」로만 보인다 —
	///   사람은 자기 인터넷을 의심하고, 우리는 그 사람이 왔었다는 것조차 모른다.
	///   가득 찬 것은 <b>고장이 아니라 상태</b>이므로, 이유를 말하고 닫는다(밀려남과 같은 예의).
	/// </summary>
	[Serializable]
	public class FullMessage
	{
		public string type = NetMessageType.FULL;
		public string reason = "세계가 가득 찼다";
		/// <summary>지금 몇 명까지 받나 — 창이 「잠시 뒤 다시」를 말할 때 쓴다.</summary>
		public int most;
	}

	/// <summary>창 → 서버: 이 칸의 건물을 부수고 싶다.</summary>
	[Serializable]
	public class RemoveMessage
	{
		public string type = NetMessageType.REMOVE;
		public int x;
		public int y;
		public int z;
	}

	/// <summary>창 → 서버: 나를 이렇게 불러 달라 (TASK-WM-218).</summary>
	[Serializable]
	public class RenameMessage
	{
		public string type = NetMessageType.RENAME;
		public string name = string.Empty;
	}

	/// <summary>서버 → 그 창에게만: 그건 안 된다(무엇을·왜).</summary>
	[Serializable]
	public class DeniedMessage
	{
		public string type = NetMessageType.DENIED;

		/// <summary>무엇을 하려 했나 — place · gather · brewcomplete · chestput …</summary>
		public string what = string.Empty;

		/// <summary>왜 안 됐나 — 사람에게 그대로 보여 줄 수 있는 짧은 말.</summary>
		public string why = string.Empty;
	}

}

