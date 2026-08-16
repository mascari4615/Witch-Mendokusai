using System;

namespace WitchMendokusai.Net
{
	/// <summary>
	/// 대결(1대1) 판에서 서버와 창이 주고받는 <b>말의 모양</b> (TASK-WM-411).
	///
	/// 세계(월드) 쪽 말과 한 파일에 섞지 않는다 — 대결은 방 단위로 짧게 살았다 사라지고,
	/// 세계는 계속 사는 하나다. 다만 <b>자리는 같다</b>: 서버·Unity·웹이 다 볼 수 있는 판정 층.
	///
	/// 흐르는 것은 <b>위치가 아니라 의도</b>다(누른 방향·겨눈 쪽). 위치는 서버가 정해서 되돌려 준다 —
	/// 그래야 두 화면이 서로 「내가 이겼다」라고 우기는 일이 없다.
	/// </summary>
	public static class VersusMessageType
	{
		/// <summary>창 → 서버: 대결 방에 끼워 줘(빈자리 있으면 짝, 없으면 새 방).</summary>
		public const string JOIN = "vsjoin";

		/// <summary>서버 → 창: 방이 찼다. 네가 몇 번인지(0/1)와 판 크기·규칙을 알려 준다.</summary>
		public const string START = "vsstart";

		/// <summary>창 → 서버: 이번 틱에 내가 하려는 것.</summary>
		public const string INPUT = "vsinput";

		/// <summary>서버 → 두 창: 지금 판의 모습(사람 둘 + 탄들). 초당 여러 번.</summary>
		public const string STATE = "vsstate";

		/// <summary>서버 → 두 창: 라운드가 끝났다(누가 이겼고 점수가 어떻게 됐나).</summary>
		public const string ROUND_END = "vsroundend";

		/// <summary>서버 → 진 창에게만: 이 중에 골라라.</summary>
		public const string OFFER = "vsoffer";

		/// <summary>창 → 서버: 몇 번째 카드로 하겠다.</summary>
		public const string PICK = "vspick";

		/// <summary>서버 → 두 창: 매치가 끝났다.</summary>
		public const string MATCH_END = "vsmatchend";

		/// <summary>서버 → 남은 창: 상대가 나갔다.</summary>
		public const string OPPONENT_LEFT = "vsleft";
	}

	/// <summary> 창 → 서버: 대결 방에 끼워 달라. </summary>
	[Serializable]
	public class VersusJoinMessage
	{
		public string type = VersusMessageType.JOIN;

		/// <summary>같이 하려는 친구와 맞춘 방 이름. 비면 아무 빈방.</summary>
		public string room = string.Empty;

		/// <summary>상대가 안 오면 봇으로 채울까 — 혼자 연습할 때 true.</summary>
		public bool fillWithBot;
	}

	/// <summary> 서버 → 창: 방이 섰다. 화면을 그 크기로 짓는 데 필요한 것 전부. </summary>
	[Serializable]
	public class VersusStartMessage
	{
		public string type = VersusMessageType.START;

		/// <summary>이 창이 0번인지 1번인지 — 화면·조작이 이걸로 갈린다.</summary>
		public int seat;

		public float halfWidth;
		public float halfDepth;
		public int roundsToWin;
		public string room = string.Empty;
	}

	/// <summary> 창 → 서버: 이번 틱의 의도. 이것 말고는 아무것도 안 보낸다. </summary>
	[Serializable]
	public class VersusInputMessage
	{
		public string type = VersusMessageType.INPUT;

		/// <summary>몇 번째 틱의 입력인가 — 늦게 온 것을 버리고 순서를 세우는 데 쓴다.</summary>
		public int tick;

		public float moveX;
		public float moveY;
		public float aimX;
		public float aimY;
		public bool fire;
		public bool dash;
	}

	/// <summary> 그릴 것 하나(사람 or 탄). </summary>
	[Serializable]
	public class VersusBodyMessage
	{
		public float x;
		public float y;
		public float r;
		public int owner;
		public bool alive;
	}

	/// <summary> 서버 → 두 창: 지금 판의 모습. 심판은 하나뿐이므로 이것이 유일한 진실이다. </summary>
	[Serializable]
	public class VersusStateMessage
	{
		public string type = VersusMessageType.STATE;
		public int tick;

		/// <summary>사람 둘. 순서 = 자리 번호.</summary>
		public VersusBodyMessage[] fighters = new VersusBodyMessage[0];

		/// <summary>날아다니는 탄 전부.</summary>
		public VersusBodyMessage[] shots = new VersusBodyMessage[0];

		public int scoreA;
		public int scoreB;
	}

	/// <summary> 서버 → 두 창: 라운드 결과. </summary>
	[Serializable]
	public class VersusRoundEndMessage
	{
		public string type = VersusMessageType.ROUND_END;

		/// <summary>이긴 자리(0/1) 또는 -1(무승부).</summary>
		public int winner = -1;

		public int scoreA;
		public int scoreB;
	}

	/// <summary> 서버 → 진 창에게만: 이 중에 골라라. </summary>
	[Serializable]
	public class VersusOfferMessage
	{
		public string type = VersusMessageType.OFFER;

		/// <summary>카드 번호들(VersusCardKind 값).</summary>
		public int[] cards = new int[0];

		/// <summary>사람에게 보여 줄 한 줄들 — 순서는 cards 와 같다.</summary>
		public string[] texts = new string[0];
	}

	/// <summary> 창 → 서버: 몇 번째로 하겠다. </summary>
	[Serializable]
	public class VersusPickMessage
	{
		public string type = VersusMessageType.PICK;
		public int index;
	}

	/// <summary> 서버 → 두 창: 매치 끝. </summary>
	[Serializable]
	public class VersusMatchEndMessage
	{
		public string type = VersusMessageType.MATCH_END;

		/// <summary>이긴 자리(0/1) 또는 -1.</summary>
		public int winner = -1;
	}
}
