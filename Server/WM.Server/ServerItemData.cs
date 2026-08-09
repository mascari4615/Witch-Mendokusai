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

	/// <summary>
	/// 서버가 아는 아이템 목록 (TASK-WM-216 → 217).
	///
	/// <b>정본은 게임 자산이다.</b> 거기서 뽑은 목록 파일(<c>items.json</c>)이 있으면 그걸 쓰고,
	/// 없으면 씨앗 둘로 돈다 — 손으로 적은 낱말표는 반드시 새기 때문에, 있는 쪽이 늘 이긴다.
	/// 파일 자리 = 환경변수 <c>WM_ITEMS_FILE</c> 또는 서버 옆 <c>items.json</c>.
	/// </summary>
	public static class ServerItemCatalog
	{
		public const int STONE = 1;
		public const int HERB = 2;

		private static readonly Dictionary<int, ServerItemData> seed = new Dictionary<int, ServerItemData>
		{
			{ STONE, new ServerItemData(STONE, 99) },
			{ HERB, new ServerItemData(HERB, 20) },
		};

		private static readonly WorldItemCatalog exported = LoadExported();

		/// <summary>
		/// 가방을 되살릴 때 쓰는 목록 (TASK-WM-218).
		/// ⚠ 전에는 「뽑아 온 목록」만 돌려줬다 — 그게 없으면 **가방이 조용히 빈 채로** 되살아났다
		///   (씨앗으로 도는 서버에서 저장은 되는데 복원만 안 되는 모양). 그래서 늘 목록을 준다.
		/// </summary>
		public static WorldItemCatalog Catalog => exported ?? seedCatalog;

		private static readonly WorldItemCatalog seedCatalog = new WorldItemCatalog(new ItemCatalogData
		{
			items = new[]
			{
				new ItemCatalogEntry { id = STONE, maxAmount = 99 },
				new ItemCatalogEntry { id = HERB, maxAmount = 20 },
			},
		});

		/// <summary>게임에서 뽑아 온 목록이 있나 — 없으면 씨앗으로 돈다.</summary>
		public static bool UsingExported => exported != null && exported.Count > 0;

		public static IItemData Find(int itemId)
		{
			if (UsingExported)
				return exported.Find(itemId);

			return seed.TryGetValue(itemId, out ServerItemData data) ? data : null;
		}

		private static WorldItemCatalog LoadExported()
		{
			string path = System.Environment.GetEnvironmentVariable("WM_ITEMS_FILE");
			if (string.IsNullOrWhiteSpace(path))
				path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "items.json");

			try
			{
				if (System.IO.File.Exists(path) == false)
					return null;

				System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
				ItemCatalogData data = System.Text.Json.JsonSerializer.Deserialize<ItemCatalogData>(System.IO.File.ReadAllText(path), options);
				WorldItemCatalog catalog = new WorldItemCatalog(data);
				System.Console.WriteLine($"[items] 게임에서 뽑은 목록 {catalog.Count}종 ({path})");
				return catalog;
			}
			catch (System.Exception error) when (error is System.IO.IOException || error is System.Text.Json.JsonException)
			{
				// 못 읽었다고 서버가 안 뜨면 그게 더 나쁘다 — 씨앗으로 돌고 알린다.
				System.Console.WriteLine("[items] 목록을 못 읽었다 — 씨앗으로 돈다: " + error.Message);
				return null;
			}
		}
	}
}
