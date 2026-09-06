using System;

namespace WitchMendokusai.Net
{
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

}


