namespace WitchMendokusai.Server
{
	/// <summary>
	/// 솥에 넣을 수 있는 재료들 (TASK-WM-217). 정본은 게임 자산이 될 것이다(후속).
	/// 지금은 <c>ingredients.json</c> 이 있으면 그것을, 없으면 <b>씨앗 넷</b>으로 돈다 —
	/// 주울 것 넷과 짝이 맞아야 「줍기 → 조리」가 한 바퀴 돈다.
	/// 파일 자리 = 환경변수 <c>WM_INGREDIENTS_FILE</c> 또는 서버 옆 <c>ingredients.json</c>.
	/// </summary>
	public static class ServerIngredients
	{
		public static WorldIngredients Shelf { get; } = Load();

		private static WorldIngredients Load()
		{
			string path = System.Environment.GetEnvironmentVariable("WM_INGREDIENTS_FILE");
			if (string.IsNullOrWhiteSpace(path))
				path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "ingredients.json");

			try
			{
				if (System.IO.File.Exists(path))
				{
					System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
					IngredientCatalogData data = System.Text.Json.JsonSerializer.Deserialize<IngredientCatalogData>(System.IO.File.ReadAllText(path), options);
					WorldIngredients shelf = new WorldIngredients(data);
					if (shelf.Count > 0)
					{
						System.Console.WriteLine($"[brew] 넣을 수 있는 재료 {shelf.Count}종 ({path})");
						return shelf;
					}
				}
			}
			catch (System.Exception error) when (error is System.IO.IOException || error is System.Text.Json.JsonException)
			{
				System.Console.WriteLine("[brew] 재료 목록을 못 읽었다 — 씨앗으로 돈다: " + error.Message);
			}

			return new WorldIngredients(WorldSeeds.Ingredients());
		}
	}
}
