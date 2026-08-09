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

	/// <summary>서버 → 그 창에게만: 네 가방은 이렇다.</summary>
	[Serializable]
	public class BagMessage
	{
		public string type = NetMessageType.BAG;
		public BagEntry[] items = Array.Empty<BagEntry>();
	}

	/// <summary>창 → 서버: 이걸 줍고 싶다.</summary>
	[Serializable]
	public class GatherMessage
	{
		public string type = NetMessageType.GATHER;
		public int itemId;
		public int amount = 1;
	}

	/// <summary>솥을 한 번 젓는 방향과 세기 — 창 → 서버.</summary>
	[Serializable]
	public class BrewMessage
	{
		public string type = NetMessageType.BREW;
		public float dx;
		public float dy;
		public float grind = 1f;
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
		public int w = 1;
		public int l = 1;
		public int buildingId;
	}
}
