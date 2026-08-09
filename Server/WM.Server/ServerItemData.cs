using System.Collections.Generic;

namespace WitchMendokusai.Server
{
	/// <summary>
	/// 서버가 아는 아이템 한 종류 (TASK-WM-216).
	///
	/// 게임 쪽 아이템 정의는 유니티 에셋(ItemData)이라 서버가 못 읽는다. 그래서 서버는
	/// <see cref="IItemData"/>(판정 층 계약)만 만족하는 얇은 구현을 쓴다 — 가방 규칙
	/// (<see cref="InventoryCore"/>)은 게임과 <b>같은 것</b>을 그대로 돌린다.
	///
	/// ⚠ 지금 목록은 손으로 적은 씨앗이다. 진짜 목록은 게임 데이터에서 뽑아 와야 한다(후속).
	/// </summary>
	public sealed class ServerItemData : IItemData
	{
		public ServerItemData(int id, int maxAmount)
		{
			ID = id;
			MaxAmount = maxAmount;
		}

		public int ID { get; }
		public int MaxAmount { get; }
		public ItemType Type => default;
		public ItemGrade Grade => default;
	}

	/// <summary>서버가 아는 아이템 목록 — 씨앗.</summary>
	public static class ServerItemCatalog
	{
		public const int STONE = 1;
		public const int HERB = 2;

		private static readonly Dictionary<int, ServerItemData> byId = new Dictionary<int, ServerItemData>
		{
			{ STONE, new ServerItemData(STONE, 99) },
			{ HERB, new ServerItemData(HERB, 20) },
		};

		public static IItemData Find(int itemId)
		{
			return byId.TryGetValue(itemId, out ServerItemData data) ? data : null;
		}
	}
}
