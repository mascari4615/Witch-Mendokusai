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

		/// <summary>서버 → 창: 마도서(무엇을 만들 수 있나·어디를 겨냥하나). 들어올 때 한 번.</summary>
		public const string SPELLBOOK = "spellbook";

		/// <summary>
		/// 서버 → 그 창에게만: <b>그건 안 된다, 왜냐면</b> (TASK-WM-217).
		/// ★ 없으면 창은 아무 말도 없이 실패한다 — 사람은 「고장났나」로 읽는다.
		/// </summary>
		public const string DENIED = "denied";

		/// <summary>창 → 서버: 그 상자 안을 보여 줘.</summary>
		public const string CHEST_ASK = "chestask";

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

	/// <summary>서버 → 그 창에게만: 그 상자 안은 이렇다.</summary>
		public const string CHEST = "chest";

		/// <summary>창 → 서버: 이걸 상자에 넣겠다(가방에서 빠진다).</summary>
		public const string CHEST_PUT = "chestput";

		/// <summary>창 → 서버: 이걸 상자에서 꺼내겠다(가방으로 들어온다).</summary>
		public const string CHEST_TAKE = "chesttake";

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

		/// <summary>
		/// 서버 → 그 창에게만: <b>네 인형은 여기 있다</b> (TASK-WM-217).
		///
		/// ★ 왜 따로 보내나: 사람이 몰린 칸에서는 소식 한 벌을 여럿이 같이 쓴다(그래야 서버가 산다).
		///   그 한 벌에는 가까운 몇 명만 들어가므로, 광장 구석에 선 사람은 <b>자기 인형</b>이 빠질 수 있다.
		///   자기가 안 보이면 화면이 통째로 멎으니, 그 사람에게만 자기 자리를 따로 알려 준다(60바이트).
		/// </summary>
		public const string ME = "me";

		/// <summary>
		/// 서버 → 창: <b>누가 무슨 이름인가</b> (TASK-WM-220).
		///
		/// ★ 왜 따로 보내나: 이름은 거의 안 바뀌는데 자리는 초당 20번 바뀐다. 이름을 자리에 얹어
		///   나르면 같은 글자를 초당 20번 나른다 — 사람 200명이면 그것만으로 판의 3분의 1이다.
		///   낱말표(catalog)와 같은 생각이다: 바뀔 때만 보내고, 창이 들고 있는다.
		/// </summary>
		public const string NAMES = "names";

		/// <summary>창 → 서버: 이걸 부수고 싶다(그 칸을 문 건물이 통째로 사라진다).</summary>
		public const string REMOVE = "remove";

		/// <summary>창 → 서버: 솥에 한 번 넣고 젓는다(모두가 같은 솥을 젓는다).</summary>
		public const string BREW = "brew";

		/// <summary>세계에 서 있는 솥 하나 — 자리와 지금 저은 자국.</summary>
	[Serializable]
	public class CauldronView
	{
		public int x;
		public int y;
		public int z;
		public float px;
		public float py;
		public int steps;
		public float side;
	}

	/// <summary>창 → 서버: 그 자리의 솥에 넣는다 / 비운다 / 가져간다.</summary>
	[Serializable]
	public class CauldronMessage
	{
		public string type = NetMessageType.BREW;
		public int itemId;
		public int x;
		public int y;
		public int z;
	}

	/// <summary>창 → 서버: 솥을 비운다.</summary>
		public const string BREW_RESET = "brewreset";

		/// <summary>창 → 서버: 이 솥을 완성으로 가져가겠다(선착순 한 번).</summary>
		public const string BREW_COMPLETE = "brewcomplete";

		/// <summary>서버 → 그 창에게만: 완성은 네 것이다(이 상태로 채점해라).</summary>
		public const string BREW_TAKEN = "brewtaken";

		/// <summary>창 → 서버: 이 줄대로 만들겠다. 재료도 주사위도 세계가 본다 (TASK-WM-217).</summary>
		public const string CRAFT = "craft";

		/// <summary>서버 → 그 창에게만: 만든 결과(됐나 · 무엇이 몇 개).</summary>
		public const string CRAFTED = "crafted";

		/// <summary>서버 → 창: 세계가 아는 제작표(들어올 때 한 번).</summary>
		public const string CRAFT_BOOK = "craftbook";

		/// <summary>창 → 서버: 나를 이렇게 불러 달라. 되나 안 되나는 세계가 본다 (TASK-WM-218).</summary>
		public const string RENAME = "rename";
	}

	/// <summary>모든 수신 메시지의 첫 판별에 쓰는 공통 envelope.</summary>
	[Serializable]
	public class NetMessageEnvelope
	{
		public string type = string.Empty;
	}

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
		public long sequence;
		public WorldDollView[] dolls = Array.Empty<WorldDollView>();

		// ★ 기본값이 <b>없음(null)</b>이다 (TASK-WM-217): 이 목록들은 바뀐 프레임에만 실린다.
		//   빈 배열로 두면 「안 실려 옴」과 「진짜로 비었음」이 구별되지 않아
		//   ① 매 프레임 집이 사라지거나 ② 마지막 하나를 부숴도 화면에 남는다.
		public BuildingView[] buildings;
		public GatherableView[] gatherables;
		public CauldronView[] cauldrons;
		public WorldTimeView time;
		public WorldBrewView brew;
	}

	/// <summary>누가 무슨 이름인가 — 바뀔 때만 온다.</summary>
	[Serializable]
	public class DollNameView
	{
		public int id;
		public string name = string.Empty;
	}

	/// <summary>서버 → 창: 이름표(바뀐 사람만). 창은 이걸 들고 있다가 인형에 붙인다.</summary>
	[Serializable]
	public class NamesMessage
	{
		public string type = NetMessageType.NAMES;
		public DollNameView[] dolls = Array.Empty<DollNameView>();
	}

	/// <summary>서버 → 그 창에게만: 네 인형은 여기 있다 (몰린 칸에서 공유 소식에 자기가 빠졌을 때).</summary>
	[Serializable]
	public class MeMessage
	{
		public string type = NetMessageType.ME;
		public WorldDollView doll;
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

	/// <summary>서버 → 그 창에게만: 그 상자 안은 이렇다.</summary>
	[Serializable]
	public class ChestView
	{
		public string type = NetMessageType.CHEST;
		public int x;
		public int y;
		public int z;
		public BagEntry[] items = Array.Empty<BagEntry>();
	}

	/// <summary>창 → 서버: 그 상자 안을 보여 줘 / 넣겠다 / 꺼내겠다.</summary>
	[Serializable]
	public class ChestMessage
	{
		public string type = NetMessageType.CHEST_ASK;
		public int x;
		public int y;
		public int z;
		public int itemId;
		public int amount = 1;
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

	/// <summary>세계에 서 있는 솥 하나 — 자리와 지금 저은 자국.</summary>
	[Serializable]
	public class CauldronView
	{
		public int x;
		public int y;
		public int z;
		public float px;
		public float py;
		public int steps;
		public float side;
	}

	/// <summary>창 → 서버: 그 자리의 솥에 넣는다 / 비운다 / 가져간다.</summary>
	[Serializable]
	public class CauldronMessage
	{
		public string type = NetMessageType.BREW;
		public int itemId;
		public int x;
		public int y;
		public int z;
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

	/// <summary>마도서 한 쪽 — 「여기까지 저으면 이게 나온다」 (TASK-WM-217).</summary>
	[Serializable]
	public class SpellbookPage
	{
		public int id;
		public string name = string.Empty;
		public float x;
		public float y;
		public float radius;
		public int itemId;
		public int amount;
	}

	/// <summary>
	/// 서버 → 창: 세계의 마도서(들어올 때 한 번).
	///
	/// ★ 왜 게임도 이걸 받아야 하나 (TASK-WM-217): 완성 보상은 세계가 정하는데 게임 화면은
	///   목표·등급을 자기 자산으로 그렸다. 둘이 어긋나면 표시대로 저은 사람이 딴 것을 받는다 —
	///   화면은 「최상급」인데 세계는 「조잡」인 상태도 만들어진다.
	/// </summary>
	[Serializable]
	public class SpellbookMessage
	{
		public string type = NetMessageType.SPELLBOOK;
		public SpellbookPage[] pages = Array.Empty<SpellbookPage>();
	}


	/// <summary>지을 수 있는 것 하나 — 세계가 보내는 모양(필드 이름이 계약이다).</summary>
	[Serializable]
	public class BuildCatalogEntryView
	{
		public int buildingId;
		public string name = string.Empty;
		public int w = 1;
		public int l = 1;
		public int costItemId;
		public int costAmount;
	}

	/// <summary>
	/// 서버 → 창: 세계가 아는 <b>지을 것 목록</b>(들어올 때 한 번).
	///
	/// ★ 왜 게임도 받아야 하나 (TASK-WM-217): 게임의 짓기 바는 자기 자산 전부를 늘어놓았다.
	///   세계가 모르는 것을 고르면 내 화면에만 섰다가 사라진다 — 사람은 「고장」으로 읽는다.
	///   재료(costItemId·costAmount)도 여기 실려야 <b>왜 안 지어지는지</b>를 보여 줄 수 있다.
	/// </summary>
	[Serializable]
	public class BuildCatalogMessage
	{
		public string type = NetMessageType.BUILD_CATALOG;
		public BuildCatalogEntryView[] buildings = Array.Empty<BuildCatalogEntryView>();
	}


	/// <summary>창 → 서버: 이 줄대로 만들겠다 (TASK-WM-217).</summary>
	[Serializable]
	public class CraftMessage
	{
		public string type = NetMessageType.CRAFT;
		public int recipeId;
	}

	/// <summary>
	/// 서버 → 그 창에게만: 만든 결과.
	///
	/// ★ 실패도 <b>말해 준다</b>: 재료는 들었는데 주사위를 진 것과, 재료가 없어 시도조차 못 한 것은
	///   사람에게 전혀 다른 일이다. 구별해서 안 보여 주면 둘 다 「고장」으로 읽힌다.
	/// </summary>
	[Serializable]
	public class CraftedMessage
	{
		public string type = NetMessageType.CRAFTED;
		public int recipeId;
		public bool attempted;
		public bool succeeded;
		public int itemId;
		public int amount;
		public string denied = string.Empty;
	}

	/// <summary>제작표 한 줄이 창에 보이는 모양 — 재료는 itemIds·amounts 짝으로 나른다.</summary>
	[Serializable]
	public class CraftBookEntryView
	{
		public int recipeId;
		public string name = string.Empty;
		public int resultItemId;
		public int resultAmount;
		public float percentage;
		public int[] itemIds = Array.Empty<int>();
		public int[] amounts = Array.Empty<int>();
	}

	/// <summary>서버 → 창: 세계가 아는 제작표(들어올 때 한 번).</summary>
	[Serializable]
	public class CraftBookMessage
	{
		public string type = NetMessageType.CRAFT_BOOK;
		public CraftBookEntryView[] recipes = Array.Empty<CraftBookEntryView>();
	}


	/// <summary>창 → 서버: 나를 이렇게 불러 달라 (TASK-WM-218).</summary>
	[Serializable]
	public class RenameMessage
	{
		public string type = NetMessageType.RENAME;
		public string name = string.Empty;
	}

}
