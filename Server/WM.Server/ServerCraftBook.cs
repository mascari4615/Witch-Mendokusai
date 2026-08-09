namespace WitchMendokusai.Server
{
	/// <summary>
	/// 세계가 든 제작표 (TASK-WM-217) — 마도서(<see cref="ServerRecipeBook"/>)와 같은 규약.
	///
	/// <b>정본은 게임 자산이다.</b> 거기서 뽑은 <c>crafts.json</c> 이 있으면 그걸 쓰고,
	/// 없으면 씨앗 두 줄로 돈다(제작이 도는지 볼 수 있어야 한다).
	/// 파일 자리 = 환경변수 <c>WM_CRAFTS_FILE</c> 또는 서버 옆 <c>crafts.json</c>.
	/// </summary>
	public static class ServerCraftBook
	{
		private static readonly WorldCraftBook loaded = Load();

		public static WorldCraftBook Book => loaded;

		/// <summary>게임에서 뽑아 온 제작표를 쓰고 있나 — 아니면 씨앗이다.</summary>
		public static bool UsingExported { get; private set; }

		private static WorldCraftBook Load()
		{
			string path = System.Environment.GetEnvironmentVariable("WM_CRAFTS_FILE");
			if (string.IsNullOrWhiteSpace(path))
				path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "crafts.json");

			try
			{
				if (System.IO.File.Exists(path))
				{
					System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
					CraftCatalogData data = System.Text.Json.JsonSerializer.Deserialize<CraftCatalogData>(System.IO.File.ReadAllText(path), options);
					WorldCraftBook book = new WorldCraftBook(data);
					if (book.Recipes.Count > 0)
					{
						UsingExported = true;
						System.Console.WriteLine($"[crafts] 게임에서 뽑은 제작표 {book.Recipes.Count}줄 ({path})");
						return book;
					}

					System.Console.WriteLine("[crafts] 뽑은 제작표가 비어 있다 — 씨앗으로 돈다: " + path);
				}
			}
			catch (System.Exception error) when (error is System.IO.IOException || error is System.Text.Json.JsonException)
			{
				// 못 읽었다고 서버가 안 뜨면 그게 더 나쁘다 — 씨앗으로 돌고 알린다.
				System.Console.WriteLine("[crafts] 제작표를 못 읽었다 — 씨앗으로 돈다: " + error.Message);
			}

			return new WorldCraftBook(WorldSeeds.Crafts());
		}
	}
}
