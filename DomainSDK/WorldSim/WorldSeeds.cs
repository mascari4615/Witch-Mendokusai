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
		public const int PLANK = 1;
		public const int STONE_BLOCK = 10;
		public const int BRICK = 15;

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

		/// <summary>
		/// 마도서 씨앗 — <b>어디서 멈추느냐</b>가 무엇이 되느냐다 (TASK-WM-217).
		///
		/// ★ 왜 여러 쪽인가: 쪽이 하나뿐이면 어떻게 저어도 같은 것만 나온다 — 「무엇을 만들까」라는
		///   고름이 없으면 조리는 버튼 누르기다. 재료 넷이 미는 방향이 서로 반대이므로,
		///   그 방향들 끝에 쪽을 놓으면 <b>무엇을 넣느냐가 곧 무엇을 만드느냐</b>가 된다.
		///
		/// 나무 → +X · 나뭇가지 → −X · 석탄 → +Y · 철광석 → −Y (한 번에 0.5씩).
		/// </summary>
		public static RecipeCatalogData Recipes()
		{
			return new RecipeCatalogData
			{
				recipes = new[]
				{
					// 이쪽저쪽 섞어 가운데로 돌아오면 — 물약.
					new RecipeCatalogEntry
					{
						id = 1, name = "치유 물약",
						targetX = 0f, targetY = 0f, radius = 0.8f,
						resultItemId = HEALING_POTION, amount = 1,
					},

					// 나무만 계속 — 판자.
					new RecipeCatalogEntry
					{
						id = 2, name = "나무 판자",
						targetX = 2f, targetY = 0f, radius = 0.8f,
						resultItemId = PLANK, amount = 2,
					},

					// 석탄 쪽으로 — 석재.
					new RecipeCatalogEntry
					{
						id = 3, name = "석재",
						targetX = 0f, targetY = 2f, radius = 0.8f,
						resultItemId = STONE_BLOCK, amount = 2,
					},

					// 철광석 쪽으로 — 벽돌.
					new RecipeCatalogEntry
					{
						id = 4, name = "벽돌",
						targetX = 0f, targetY = -2f, radius = 0.8f,
						resultItemId = BRICK, amount = 2,
					},
				},
			};
		}

		/// <summary>
		/// 이 건물은 무엇으로 짓나 (TASK-WM-217) — 씨앗 규칙.
		/// 목록에 적힌 게 있으면 그것, 없으면 나무(처음 온 사람도 지을 수 있게).
		/// </summary>
		public static int CostItemOf(int buildingId)
		{
			BuildingCatalogData seeds = Buildings();
			for (int i = 0; i < seeds.buildings.Length; i++)
			{
				if (seeds.buildings[i].id == buildingId)
					return seeds.buildings[i].costItemId;
			}

			return WOOD;
		}

		/// <summary>지을 것 씨앗 — 뽑아 둔 목록이 없을 때도 뭔가는 지어 볼 수 있어야 한다.</summary>
		public static BuildingCatalogData Buildings()
		{
			return new BuildingCatalogData
			{
				buildings = new[]
				{
					// ★ 만든 것이 <b>다음 것을 짓는 재료</b>가 된다 (TASK-WM-217):
					//   나무 → (솥에서) 판자 → 더 좋은 건물. 사슬이 없으면 조리는 막다른 길이다.
					//   상자만은 나무로 짓는다 — 처음 온 사람이 아무것도 없이 시작하기 때문이다.
					new BuildingCatalogEntry { id = 4005, name = "보관 상자", w = 1, l = 1, slots = 30, costItemId = WOOD, costAmount = 2 },
					// ⚠ 솥은 <b>나무</b>로 짓는다: 판자는 솥에서 나오므로, 솥을 판자로 지으면
					//   「솥이 있어야 판자, 판자가 있어야 솥」인 닭·달걀이 된다(아무도 시작 못 한다).
					new BuildingCatalogEntry { id = 4000, name = "솥", w = 1, l = 1, costItemId = WOOD, costAmount = 2 },
					new BuildingCatalogEntry { id = 4001, name = "모루", w = 1, l = 1, costItemId = STONE_BLOCK, costAmount = 2 },
				},
			};
		}

		/// <summary>
		/// 씨앗 제작표 (TASK-WM-217) — 진짜 자산을 뽑기 전까지 <b>제작이 도는지</b> 볼 수 있어야 한다.
		/// 솥과 갈라지는 자리다: 솥은 「저어서」, 제작은 「재료를 모아서」.
		/// </summary>
		public static CraftCatalogData Crafts()
		{
			return new CraftCatalogData
			{
				recipes = new[]
				{
					// ★ 제작 줄의 번호 = <b>결과 아이템 번호</b> (TASK-WM-217).
					//   게임 화면은 「이 아이템을 만들겠다」로 고르고, 세계는 그 번호로 줄을 찾는다.
					//   따로 매기면 자산에서 뽑을 때마다 번호가 흔들려 어제 되던 제작이 오늘 안 된다.
					// 나무 셋 → 판자 둘. 반드시 된다(제작이 도는지 보는 줄).
					new CraftRecipeEntry
					{
						id = PLANK, name = "나무 판자", resultItemId = PLANK, resultAmount = 2, percentage = 100f,
						items = new[] { new CraftIngredientEntry { itemId = WOOD, amount = 3 } },
					},

					// 석탄 + 철 → 벽돌. 가끔 실패한다(주사위가 세계에 있는지 보는 줄).
					new CraftRecipeEntry
					{
						id = BRICK, name = "벽돌", resultItemId = BRICK, resultAmount = 1, percentage = 70f,
						items = new[]
						{
							new CraftIngredientEntry { itemId = COAL, amount = 1 },
							new CraftIngredientEntry { itemId = IRON, amount = 1 },
						},
					},
				},
			};
		}

	}
}
