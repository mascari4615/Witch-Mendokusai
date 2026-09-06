using System;

namespace WitchMendokusai.Net
{
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

}


