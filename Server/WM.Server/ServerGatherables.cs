namespace WitchMendokusai.Server
{
	/// <summary>
	/// 이 세계에 무엇이 자라는가 (TASK-WM-217).
	///
	/// 정본은 게임 자산이 될 것이다(후속). 지금은 <c>gatherables.json</c> 이 있으면 그것을,
	/// 없으면 <b>씨앗</b>으로 돈다 — 빈 들판이면 「줍기 → 가방 → 조리」가 아예 안 돌아
	/// 놀 수 있는지 볼 수가 없다.
	/// 파일 자리 = 환경변수 <c>WM_GATHERABLES_FILE</c> 또는 서버 옆 <c>gatherables.json</c>.
	/// </summary>
	public static class ServerGatherables
	{
		/// <summary>씨앗에 쓰는 아이템 번호 — 게임 목록의 진짜 재료들(나무·나뭇가지·석탄·철광석).</summary>
		private const int WOOD = 0;
		private const int BRANCH = 2;
		private const int COAL = 4;
		private const int IRON = 5;

		public static WorldGatherables Field { get; } = Load();

		private static WorldGatherables Load()
		{
			string path = System.Environment.GetEnvironmentVariable("WM_GATHERABLES_FILE");
			if (string.IsNullOrWhiteSpace(path))
				path = System.IO.Path.Combine(System.AppContext.BaseDirectory, "gatherables.json");

			try
			{
				if (System.IO.File.Exists(path))
				{
					System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
					GatherableKind[] kinds = System.Text.Json.JsonSerializer.Deserialize<GatherableKind[]>(System.IO.File.ReadAllText(path), options);
					WorldGatherables field = new WorldGatherables(kinds);
					if (field.KindCount > 0)
					{
						System.Console.WriteLine($"[gather] 자라는 것 {field.KindCount}종 ({path})");
						return field;
					}
				}
			}
			catch (System.Exception error) when (error is System.IO.IOException || error is System.Text.Json.JsonException)
			{
				System.Console.WriteLine("[gather] 목록을 못 읽었다 — 씨앗으로 돈다: " + error.Message);
			}

			return new WorldGatherables(new[]
			{
				new GatherableKind { itemId = WOOD, amount = 2, respawnMinutes = 180 },
				new GatherableKind { itemId = BRANCH, amount = 3, respawnMinutes = 120 },
				new GatherableKind { itemId = COAL, amount = 1, respawnMinutes = 300 },
				new GatherableKind { itemId = IRON, amount = 1, respawnMinutes = 360 },
			});
		}
	}
}
