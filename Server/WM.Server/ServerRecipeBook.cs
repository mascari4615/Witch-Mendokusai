namespace WitchMendokusai.Server
{
	/// <summary>
	/// 세계가 든 마도서 (TASK-WM-217).
	///
	/// <b>정본은 게임 자산이다.</b> 거기서 뽑은 <c>recipes.json</c> 이 있으면 그걸 쓰고,
	/// 없으면 <b>씨앗 한 쪽</b>으로 돈다 — 마도서가 비면 아무리 저어도 아무것도 안 나오고,
	/// 그러면 「완성」이 있으나 마나다(놀 수 있는지 볼 수가 없다).
	/// 파일 자리 = 환경변수 <c>WM_RECIPES_FILE</c> 또는 서버 옆 <c>recipes.json</c>.
	/// </summary>
	public static class ServerRecipeBook
	{
		private static readonly WorldRecipeBook loaded = Load();

		public static WorldRecipeBook Book => loaded;

		/// <summary>게임에서 뽑아 온 마도서를 쓰고 있나 — 아니면 씨앗이다.</summary>
		public static bool UsingExported { get; private set; }

		private static WorldRecipeBook Load()
		{
			string path = System.Environment.GetEnvironmentVariable("WM_RECIPES_FILE");
			if (string.IsNullOrWhiteSpace(path))
				path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "recipes.json");

			try
			{
				if (System.IO.File.Exists(path))
				{
					System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
					RecipeCatalogData data = System.Text.Json.JsonSerializer.Deserialize<RecipeCatalogData>(System.IO.File.ReadAllText(path), options);
					WorldRecipeBook book = new WorldRecipeBook(data);
					if (book.Count > 0)
					{
						UsingExported = true;
						System.Console.WriteLine($"[recipes] 게임에서 뽑은 마도서 {book.Count}쪽 ({path})");
						return book;
					}

					System.Console.WriteLine("[recipes] 뽑은 마도서가 비어 있다 — 씨앗으로 돈다: " + path);
				}
			}
			catch (System.Exception error) when (error is System.IO.IOException || error is System.Text.Json.JsonException)
			{
				// 못 읽었다고 서버가 안 뜨면 그게 더 나쁘다 — 씨앗으로 돌고 알린다.
				System.Console.WriteLine("[recipes] 마도서를 못 읽었다 — 씨앗으로 돈다: " + error.Message);
			}

			return Seed();
		}

		/// <summary>
		/// 씨앗 마도서 — 원점 근처로 저으면 「치유 물약」(17450) 한 병.
		/// 진짜 마도서가 나올 때까지 <b>솥이 손에 뭔가를 쥐여 주는지</b> 확인할 수 있어야 한다.
		/// </summary>
		private static WorldRecipeBook Seed() => new WorldRecipeBook(WorldSeeds.Recipes());
	}
}
