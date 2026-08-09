namespace WitchMendokusai.Server
{
	/// <summary>
	/// 세계가 아는 건물 목록 (TASK-WM-217). 정본은 게임 자산 — 거기서 뽑은 <c>buildings.json</c>.
	/// 없으면 <b>씨앗 한 종</b>으로 돈다(아무것도 못 지으면 놀 수 있는지 볼 수가 없다).
	/// 파일 자리 = 환경변수 <c>WM_BUILDINGS_FILE</c> 또는 서버 옆 <c>buildings.json</c>.
	/// </summary>
	public static class ServerBuildingCatalog
	{
		public static WorldBuildingCatalog Catalog { get; } = Load();

		private static WorldBuildingCatalog Load()
		{
			string path = System.Environment.GetEnvironmentVariable("WM_BUILDINGS_FILE");
			if (string.IsNullOrWhiteSpace(path))
				path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "buildings.json");

			try
			{
				if (System.IO.File.Exists(path))
				{
					System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
					BuildingCatalogData data = System.Text.Json.JsonSerializer.Deserialize<BuildingCatalogData>(System.IO.File.ReadAllText(path), options);
					WorldBuildingCatalog catalog = new WorldBuildingCatalog(data);
					if (catalog.Count > 0)
					{
						System.Console.WriteLine($"[buildings] 지을 것 {catalog.Count}종 ({path})");
						return catalog;
					}
				}
			}
			catch (System.Exception error) when (error is System.IO.IOException || error is System.Text.Json.JsonException)
			{
				System.Console.WriteLine("[buildings] 목록을 못 읽었다 — 씨앗으로 돈다: " + error.Message);
			}

			return new WorldBuildingCatalog(new BuildingCatalogData
			{
				buildings = new[]
				{
					new BuildingCatalogEntry { id = 4000, name = "솥", w = 1, l = 1 },
					new BuildingCatalogEntry { id = 4001, name = "모루", w = 1, l = 1 },
				},
			});
		}
	}
}
