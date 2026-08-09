using System;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 서버와 창이 주고받는 <b>말의 모양</b> — 판정 층에 둔다 (TASK-WM-216).
	///
	/// 왜 여기인가: 이 모양을 서버·Unity·웹이 각자 적으면 반드시 갈라진다.
	/// DomainSDK 는 <b>셋 다 볼 수 있는 유일한 자리</b>다(서버는 참조, Unity 는 같은 소스,
	/// 웹은 여기서 뽑은 타입 선언).
	///
	/// 필드가 <c>public</c> 인 이유: Unity 의 JsonUtility 가 그렇게만 읽는다.
	/// </summary>
	public static class NetMessageType
	{
		public const string WELCOME = "welcome";
		public const string WORLD = "world";
		public const string MOVE = "move";
	}

	/// <summary>서버 → 창: 접속했다, 네 인형 번호는 이것이다.</summary>
	[Serializable]
	public class WelcomeMessage
	{
		public string type = NetMessageType.WELCOME;
		public int id;
	}

	/// <summary>세계에 있는 인형 하나 — 창이 그리는 데 필요한 최소.</summary>
	[Serializable]
	public class DollView
	{
		public int id;
		public float x;
		public float z;
	}

	/// <summary>서버 → 창: 지금 세계는 이렇게 생겼다.</summary>
	[Serializable]
	public class WorldMessage
	{
		public string type = NetMessageType.WORLD;
		public DollView[] dolls = Array.Empty<DollView>();
	}

	/// <summary>창 → 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다).</summary>
	[Serializable]
	public class MoveMessage
	{
		public string type = NetMessageType.MOVE;
		public float x;
		public float z;
	}
}
