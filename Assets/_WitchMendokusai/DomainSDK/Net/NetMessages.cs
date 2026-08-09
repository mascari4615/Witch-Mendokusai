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
		/// <summary>창 → 서버: 나 왔다(열쇠가 있으면 같이). 첫 말이다.</summary>
		public const string HELLO = "hello";

		public const string WELCOME = "welcome";
		public const string WORLD = "world";
		public const string MOVE = "move";

		/// <summary>창 → 서버: 여기에 짓고 싶다(겹치는지는 서버가 본다).</summary>
		public const string PLACE = "place";

		/// <summary>창 → 서버: 이걸 줍고 싶다(가방에 들어갈지는 서버가 본다).</summary>
		public const string GATHER = "gather";

		/// <summary>서버 → 그 창에게만: 네 가방은 이렇다.</summary>
		public const string BAG = "bag";

		/// <summary>창 → 서버: 내 가방 좀 알려줘 (TASK-WM-218 — 다시 들어왔을 때 화면을 채우려면 물어봐야 한다).</summary>
		public const string BAG_ASK = "bagask";

		/// <summary>
		/// 서버 → 창: 아이템 번호↔이름 낱말표 (들어올 때 한 번).
		/// ★ 왜 한 번인가: 가방이 바뀔 때마다 이름을 같이 보내면 같은 낱말을 초당 몇 번씩 나른다.
		///   낱말표는 세계가 도는 동안 안 바뀐다 — 한 번 주고, 그 뒤로는 번호만 나른다.
		/// </summary>
		public const string CATALOG = "catalog";

		/// <summary>서버 → 창: 지을 수 있는 것 목록(들어올 때 한 번). 크기의 정본은 세계다.</summary>
		public const string BUILD_CATALOG = "buildcatalog";

		/// <summary>서버 → 창: 솥에 넣을 수 있는 재료 목록(들어올 때 한 번).</summary>
		public const string BREW_SHELF = "brewshelf";

		/// <summary>창 → 서버: 이걸 썼다(제작 재료 등). 안 알리면 쓴 게 다시 생긴다.</summary>
		public const string CONSUME = "consume";

		/// <summary>창 → 서버: 다른 기기를 이을 초대 열쇠를 만들어 줘.</summary>
		public const string INVITE_ASK = "inviteask";

		/// <summary>서버 → 그 창에게만: 초대 열쇠는 이것이다(한 번만 쓴다).</summary>
		public const string INVITE = "invite";

		/// <summary>창 → 서버: 이 초대 열쇠로 나를 그 사람에 이어 줘.</summary>
		public const string LINK = "link";

		/// <summary>서버 → 그 창에게만: 이었다(또는 못 이었다).</summary>
		public const string LINKED = "linked";

		/// <summary>서버 → 그 창에게만: 다른 곳에서 같은 사람이 들어왔다(여기서는 나간다).</summary>
		public const string KICKED = "kicked";

		/// <summary>창 → 서버: 이걸 부수고 싶다(그 칸을 문 건물이 통째로 사라진다).</summary>
		public const string REMOVE = "remove";

		/// <summary>창 → 서버: 솥에 한 번 넣고 젓는다(모두가 같은 솥을 젓는다).</summary>
		public const string BREW = "brew";

		/// <summary>창 → 서버: 솥을 비운다.</summary>
		public const string BREW_RESET = "brewreset";

		/// <summary>창 → 서버: 이 솥을 완성으로 가져가겠다(선착순 한 번).</summary>
		public const string BREW_COMPLETE = "brewcomplete";

		/// <summary>서버 → 그 창에게만: 완성은 네 것이다(이 상태로 채점해라).</summary>
		public const string BREW_TAKEN = "brewtaken";
	}

	/// <summary>
	/// 창 → 서버: 나 왔다 (TASK-WM-218). 열쇠가 있으면 같이 낸다 —
	/// 없거나 모르는 열쇠면 세계가 <b>새 사람</b>으로 받고 새 열쇠를 준다(남의 것은 안 준다).
	/// </summary>
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

	/// <summary>세계에 있는 인형 하나 — 창이 그리는 데 필요한 최소.</summary>
	[Serializable]
	public class WorldDollView
	{
		public int id;
		public float x;
		public float z;

		/// <summary>세계에서 불리는 이름 (TASK-WM-218) — 손님이면 「손님 N」.</summary>
		public string name = string.Empty;
	}

	/// <summary>
	/// 세계의 시각 — <b>서버가 굴린다</b> (TASK-WM-217). 창은 받아서 보여 주기만 한다.
	/// 시계가 호스트에 매달려 있으면 그 사람이 나갈 때 세계의 시간이 멈춘다.
	/// </summary>
	[Serializable]
	public class WorldTimeView
	{
		public int year = 1;
		public int season;
		public int day = 1;
		public int hour;
		public int minute;
	}

	/// <summary>서버 → 창: 지금 세계는 이렇게 생겼다.</summary>
	[Serializable]
	public class WorldMessage
	{
		public string type = NetMessageType.WORLD;
		public WorldDollView[] dolls = Array.Empty<WorldDollView>();
		public BuildingView[] buildings = Array.Empty<BuildingView>();
		public GatherableView[] gatherables = Array.Empty<GatherableView>();
		public WorldTimeView time;
		public WorldBrewView brew;
	}

	/// <summary>창 → 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다).</summary>
	[Serializable]
	public class MoveMessage
	{
		public string type = NetMessageType.MOVE;
		public float x;
		public float z;
	}

	/// <summary>세계에 서 있는 건물 하나 — 창이 그리는 데 필요한 최소.</summary>
	[Serializable]
	public class BuildingView
	{
		public int x;
		public int y;
		public int z;
		public int w;
		public int l;
		public int buildingId;
	}

	/// <summary>가방 안 한 종류 — 몇 개 있나.</summary>
	[Serializable]
	public class BagEntry
	{
		public int itemId;
		public int amount;
	}

	/// <summary>낱말표 한 줄 — 이 번호는 이렇게 부른다.</summary>
	[Serializable]
	public class CatalogEntry
	{
		public int itemId;
		public string name = string.Empty;
	}

	/// <summary>서버 → 창: 아이템 낱말표(들어올 때 한 번).</summary>
	[Serializable]
	public class CatalogMessage
	{
		public string type = NetMessageType.CATALOG;
		public CatalogEntry[] items = Array.Empty<CatalogEntry>();
	}

	/// <summary>서버 → 그 창에게만: 네 가방은 이렇다.</summary>
	[Serializable]
	public class BagMessage
	{
		public string type = NetMessageType.BAG;
		public BagEntry[] items = Array.Empty<BagEntry>();
	}

	/// <summary>창 → 서버: 이걸 썼다.</summary>
	[Serializable]
	public class ConsumeMessage
	{
		public string type = NetMessageType.CONSUME;
		public int itemId;
		public int amount = 1;
	}

	/// <summary>창 → 서버: 내 가방 좀 알려줘.</summary>
	[Serializable]
	public class BagAskMessage
	{
		public string type = NetMessageType.BAG_ASK;
	}

	/// <summary>창 → 서버: 이걸 줍고 싶다.</summary>
	[Serializable]
	public class GatherMessage
	{
		public string type = NetMessageType.GATHER;

		/// <summary>세계에 서 있는 그것의 번호 — 무엇이 몇 개 나오는지는 세계가 안다.</summary>
		public int nodeId;
	}

	/// <summary>세계에 서 있는 주울 것 하나 — 창이 그리는 데 필요한 최소.</summary>
	[Serializable]
	public class GatherableView
	{
		public int id;
		public float x;
		public float z;
		public int itemId;
		public int amount;
	}

	/// <summary>솥을 한 번 젓는 방향과 세기 — 창 → 서버.</summary>
	[Serializable]
	public class BrewMessage
	{
		public string type = NetMessageType.BREW;

		/// <summary>넣을 재료 — 가방에서 실제로 빠진다. 미는 방향은 세계가 안다.</summary>
		public int itemId;
	}

	/// <summary>창 → 서버: 솥을 비운다.</summary>
	[Serializable]
	public class BrewResetMessage
	{
		public string type = NetMessageType.BREW_RESET;
	}

	/// <summary>창 → 서버: 이 솥을 완성으로 가져가겠다.</summary>
	[Serializable]
	public class BrewCompleteMessage
	{
		public string type = NetMessageType.BREW_COMPLETE;
	}

	/// <summary>
	/// 서버 → 그 창에게만: <b>완성은 네 것이다</b> (TASK-WM-217).
	/// 둘이 같은 순간에 눌러도 이 말은 한 사람에게만 간다 — 이중지급이 구조적으로 불가능하다.
	/// </summary>
	[Serializable]
	public class BrewTakenMessage
	{
		public string type = NetMessageType.BREW_TAKEN;
		public float x;
		public float y;
		public int steps;
		public float side;

		// 무엇이 나왔는지도 세계가 정한다 (TASK-WM-217). itemId 0 = 아무 쪽에도 못 닿았다.
		// ★ 이 값이 오기 전에는 게임이 자기 레시피로 다시 채점하고 **또 인벤토리에 넣었다** — 이중지급.
		public int itemId;
		public int amount;
		public int grade;
		public string recipe = string.Empty;
	}

	/// <summary>솥에 저은 한 걸음 — 경로선을 그리는 쪽이 읽어 간다.</summary>
	[Serializable]
	public class BrewStepView
	{
		public float dx;
		public float dy;
		public float grind = 1f;
	}

	/// <summary>
	/// 지금 솥의 모습 — <b>모두가 같은 솥을 본다</b> (TASK-WM-217).
	/// 호스트가 갖고 있으면 그 사람이 나갈 때 젓던 게 사라진다.
	/// </summary>
	[Serializable]
	public class WorldBrewView
	{
		public float x;
		public float y;
		public int steps;
		public float side;
		public BrewStepView[] path = Array.Empty<BrewStepView>();

		// 완성으로 받아 온 것일 때만 채워진다(그냥 보고 있는 솥에는 없다).
		public int itemId;
		public int amount;
		public int grade;
		public string recipe = string.Empty;
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

	/// <summary>창 → 서버: 이 칸의 건물을 부수고 싶다.</summary>
	[Serializable]
	public class RemoveMessage
	{
		public string type = NetMessageType.REMOVE;
		public int x;
		public int y;
		public int z;
	}

	/// <summary>창 → 서버: 여기에 짓고 싶다. 겹치면 서버가 거절한다.</summary>
	[Serializable]
	public class PlaceMessage
	{
		public string type = NetMessageType.PLACE;
		public int x;
		public int y;
		public int z;

		/// <summary>무엇을 짓나 — 크기는 세계가 안다(창이 「이건 1×1 이다」로 우기던 길은 없앴다).</summary>
		public int buildingId;
	}
}
