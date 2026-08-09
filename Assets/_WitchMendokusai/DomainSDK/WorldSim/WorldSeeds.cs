namespace WitchMendokusai
{
	/// <summary>
	/// 세계의 <b>씨앗</b> — 뽑아 둔 목록이 없을 때 쓰는 최소 한 벌 (TASK-WM-217).
	///
	/// ★ 왜 판정 층인가: 서버와 「내 안의 세계」가 각자 씨앗을 적으면 <b>혼자 놀 때와 같이 놀 때가
	///   갈라진다</b> — 같은 나무를 주웠는데 한쪽에서만 물약이 되는 식으로. 씨앗도 규칙이다.
	///
	/// 아이템 번호는 게임 자산의 진짜 번호(0 나무 · 2 나뭇가지 · 4 석탄 · 5 철광석 · 17450 치유 물약).
	/// </summary>
	public static class WorldSeeds
	{
		public const int WOOD = 0;
		public const int BRANCH = 2;
		public const int COAL = 4;
		public const int IRON = 5;
		public const int HEALING_POTION = 17450;

		/// <summary>땅에서 자라는 것들 — 넷을 네 방향 재료로 쓴다.</summary>
		public static GatherableKind[] Gatherables()
		{
			return new[]
			{
				new GatherableKind { itemId = WOOD, amount = 2, respawnMinutes = 180 },
				new GatherableKind { itemId = BRANCH, amount = 3, respawnMinutes = 120 },
				new GatherableKind { itemId = COAL, amount = 1, respawnMinutes = 300 },
				new GatherableKind { itemId = IRON, amount = 1, respawnMinutes = 360 },
			};
		}

		/// <summary>솥에 넣을 수 있는 것들 — 서로 반대 방향이라 섞는 만큼 가운데로 돌아온다.</summary>
		public static IngredientCatalogData Ingredients()
		{
			return new IngredientCatalogData
			{
				ingredients = new[]
				{
					new IngredientCatalogEntry { itemId = WOOD, name = "나무", dx = 1f, dy = 0f, grind = 0.5f },
					new IngredientCatalogEntry { itemId = BRANCH, name = "나뭇가지", dx = -1f, dy = 0f, grind = 0.5f },
					new IngredientCatalogEntry { itemId = COAL, name = "석탄", dx = 0f, dy = 1f, grind = 0.5f },
					new IngredientCatalogEntry { itemId = IRON, name = "철광석", dx = 0f, dy = -1f, grind = 0.5f },
				},
			};
		}

		/// <summary>마도서 씨앗 — 가운데 근처에서 멈추면 치유 물약.</summary>
		public static RecipeCatalogData Recipes()
		{
			return new RecipeCatalogData
			{
				recipes = new[]
				{
					new RecipeCatalogEntry
					{
						id = 1,
						name = "치유 물약",
						targetX = 0f,
						targetY = 0f,
						radius = 1.5f,
						resultItemId = HEALING_POTION,
						amount = 1,
					},
				},
			};
		}

		/// <summary>지을 것 씨앗 — 뽑아 둔 목록이 없을 때도 뭔가는 지어 볼 수 있어야 한다.</summary>
		public static BuildingCatalogData Buildings()
		{
			return new BuildingCatalogData
			{
				buildings = new[]
				{
					new BuildingCatalogEntry { id = 4000, name = "솥", w = 1, l = 1 },
					new BuildingCatalogEntry { id = 4001, name = "모루", w = 1, l = 1 },
				},
			};
		}
	}
}
