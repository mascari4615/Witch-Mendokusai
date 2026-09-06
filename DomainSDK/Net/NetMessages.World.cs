using System;

namespace WitchMendokusai.Net
{
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

		/// <summary>참이면 <b>바뀐 사람만</b> 실려 있다 (TASK-WM-220) — 안 실린 사람은 그 자리 그대로.</summary>
		public bool changed;

		/// <summary>이제 안 보이는 사람들 — 창에서 지운다.</summary>
		public int[] gone;

		public WorldDollView[] dolls = Array.Empty<WorldDollView>();

		// ★ 기본값이 <b>없음(null)</b>이다 (TASK-WM-217): 이 목록들은 바뀐 프레임에만 실린다.
		//   빈 배열로 두면 「안 실려 옴」과 「진짜로 비었음」이 구별되지 않아
		//   ① 매 프레임 집이 사라지거나 ② 마지막 하나를 부숴도 화면에 남는다.
		public BuildingView[] buildings;
		public GatherableView[] gatherables;
		public CauldronView[] cauldrons;
		public WorldTimeView time;
		public WorldBrewView brew;

		/// <summary>
		/// 들판이 <b>바뀐 자리만</b> 실려 왔는가 (TASK-WM-220).
		///
		/// ★ 창(`WebWorldClient.MergeField`)이 이 둘을 읽는데 여기 정의가 없어 **master 가
		///   컴파일되지 않았다**(2026-08-12, 플레이어 빌드 CS1061 다섯 개). 쓰는 쪽만 올라오고
		///   담는 그릇이 안 올라온 상태였다 — 서버는 아직 이 값을 안 보내므로, 기본값
		///   (false · null)이 그대로 「통째로 왔다」를 뜻해 지금 동작은 달라지지 않는다.
		/// </summary>
		public bool fieldChanged;

		/// <summary>사라진 들판 항목의 번호들. null = 이번 판에는 없앤 것이 없다.</summary>
		public int[] fieldGone;
	}

	/// <summary>창 → 서버: 이쪽으로 가고 싶다(얼마나 갈지는 서버가 정한다).</summary>
	[Serializable]
	public class MoveMessage
	{
		public string type = NetMessageType.MOVE;
		public float x;
		public float z;
	}

	/// <summary>창 → 서버: 내가 지금 이 사람들을 그리고 있다 (TASK-WM-329).</summary>
	[Serializable]
	public class RosterMessage
	{
		public string type = NetMessageType.ROSTER;

		/// <summary>지금 화면에 그리고 있는 사람 번호들.</summary>
		public int[] ids = Array.Empty<int>();
	}

	/// <summary>서버 → 그 창에게만: 이 번호들은 여기 없다 — 지워라 (TASK-WM-329).</summary>
	[Serializable]
	public class GhostsMessage
	{
		public string type = NetMessageType.GHOSTS;

		public int[] ids = Array.Empty<int>();
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

	/// <summary>
	/// 세계 → 창: <b>여기부터는 저 세계다</b> (TASK-WM-254·261).
	/// 창은 통행증을 들고 저 주소에 hello 한다 — 안 다루면 국경에서 그 창만 멈춰 선다.
	/// </summary>
	[Serializable]
	public class MoveOnMessage
	{
		public string type = NetMessageType.MOVE_ON;
		public string zone = string.Empty;
		public string address = string.Empty;
		public float x;
		public float z;
		public string pass = string.Empty;
	}

	/// <summary>창 → 세계: 저 사람을 때린다 (TASK-WM-251·261). 되는지는 세계가 본다.</summary>
	[Serializable]
	public class StrikeMessage
	{
		public string type = NetMessageType.STRIKE;
		public int targetId;
	}

	/// <summary>
	/// 세계 → 창: 누가 맞았다 — 남은 몸과 <b>쓰러졌는지</b>.
	/// ⚠ 창이 스스로 몸을 셈하면 세계와 갈라진다 — 몸은 이 말로만 안다.
	/// </summary>
	[Serializable]
	public class HurtMessage
	{
		public string type = NetMessageType.HURT;
		public int dollId;
		public int by;
		public int health;
		public bool down;
	}

	/// <summary>창 → 세계: 이렇게 말했다 (TASK-WM-250·261). 다듬는 것은 세계가 한다(SaidLine).</summary>
	[Serializable]
	public class SayMessage
	{
		public string type = NetMessageType.SAY;
		public string text = string.Empty;
	}

	/// <summary>세계 → 창: 누가 이렇게 말했다 — <b>보이는 사람에게만</b> 온다.</summary>
	[Serializable]
	public class SaidMessage
	{
		public string type = NetMessageType.SAID;
		public int dollId;
		public string name = string.Empty;
		public string text = string.Empty;
	}

}


