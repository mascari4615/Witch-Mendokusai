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

		/// <summary>창 → 서버: 여기에 짓고 싶다(겹치는지는 서버가 본다).</summary>
		public const string PLACE = "place";

		/// <summary>창 → 서버: 이걸 줍고 싶다(가방에 들어갈지는 서버가 본다).</summary>
		public const string GATHER = "gather";

		/// <summary>서버 → 그 창에게만: 네 가방은 이렇다.</summary>
		public const string BAG = "bag";
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
