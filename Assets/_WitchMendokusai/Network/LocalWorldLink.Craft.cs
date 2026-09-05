using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	// LocalWorldLink 의 만들기와 상자 부분. 같은 클래스의 partial 조각이다. 상태(필드)는 LocalWorldLink.cs 를 본다.
	public sealed partial class LocalWorldLink : IWorldLink
	{
		/// <summary>마지막으로 들여다본 상자 — 혼자 놀 때도 같은 규약이다.</summary>
		public ChestView Chest { get; private set; }

		/// <summary>
		/// 내 안의 세계가 아는 제작표 — <b>같은 규칙</b>으로 판정한다 (TASK-WM-217).
		/// 혼자 놀 때만 창이 굴리면 혼자/같이가 또 갈라진다.
		/// </summary>
		public CraftBookEntryView[] CraftBook
		{
			get
			{
				System.Collections.Generic.IReadOnlyList<CraftRecipeEntry> recipes = CraftBookOf.Loaded.Recipes;
				CraftBookEntryView[] view = new CraftBookEntryView[recipes.Count];
				for (int i = 0; i < recipes.Count; i++)
				{
					CraftRecipeEntry recipe = recipes[i];
					CraftIngredientEntry[] items = recipe.items ?? System.Array.Empty<CraftIngredientEntry>();

					int[] itemIds = new int[items.Length];
					int[] amounts = new int[items.Length];
					for (int need = 0; need < items.Length; need++)
					{
						itemIds[need] = items[need].itemId;
						amounts[need] = items[need].amount;
					}

					view[i] = new CraftBookEntryView
					{
						recipeId = recipe.id, name = recipe.name,
						resultItemId = recipe.resultItemId, resultAmount = recipe.resultAmount,
						percentage = recipe.percentage <= 0f ? 100f : recipe.percentage,
						itemIds = itemIds, amounts = amounts,
					};
				}

				return view;
			}
		}

		private CraftedMessage crafted;

		/// <summary>
		/// 혼자 놀아도 <b>세계가 판정한다</b> (TASK-WM-217) — 재료를 쓰고, 주사위를 굴리고, 넣어 준다.
		/// 실패해도 재료는 든다(그게 주사위를 굴리는 값이다).
		/// </summary>
		public void RequestCraft(int recipeId)
		{
			CraftResult judged = CraftBookOf.Loaded.Judge(
				recipeId,
				itemId => world.BagCount(me.Id, itemId),
				UnityEngine.Random.Range(0f, 100f));

			if (judged.Attempted == false)
			{
				crafted = Result(judged);
				return;
			}

			// 받을 자리부터 본다 — 만들고 나서 못 받으면 재료만 사라진다.
			if (judged.Succeeded
				&& world.CanReceive(me.Id, ItemCatalog.Find(judged.ResultItemId), judged.ResultAmount) == false)
			{
				crafted = new CraftedMessage
				{
					recipeId = recipeId, attempted = false, succeeded = false,
					denied = "가방이 꽉 찼다 — 비우고 다시 오면 재료는 그대로다",
				};

				return;
			}

			CraftRecipeEntry recipe = CraftBookOf.Loaded.Find(recipeId);
			CraftIngredientEntry[] items = recipe.items ?? System.Array.Empty<CraftIngredientEntry>();
			for (int i = 0; i < items.Length; i++)
			{
				if (items[i] == null || items[i].amount <= 0)
					continue;

				world.TryConsume(me.Id, items[i].itemId, items[i].amount);
			}

			if (judged.Succeeded)
				world.TryGather(me.Id, ItemCatalog.Find(judged.ResultItemId), judged.ResultAmount);

			crafted = Result(judged);
		}

		public CraftedMessage TakeCraftResult()
		{
			CraftedMessage taken = crafted;
			crafted = null;
			return taken;
		}

		private static CraftedMessage Result(CraftResult judged)
		{
			return new CraftedMessage
			{
				recipeId = judged.RecipeId, attempted = judged.Attempted, succeeded = judged.Succeeded,
				itemId = judged.ResultItemId, amount = judged.ResultAmount, denied = judged.Denied ?? string.Empty,
			};
		}

		/// <summary>혼자 노는 세계도 이름을 안다 — 게임 자산이 정본이라 여기선 빈 목록으로 둔다.</summary>
		public CatalogEntry[] ItemNames => System.Array.Empty<CatalogEntry>();

		public SpellbookPage[] Spellbook
		{
			get
			{
				System.Collections.Generic.IReadOnlyList<RecipeCatalogEntry> pages = RecipeBook.Loaded.Pages;
				SpellbookPage[] view = new SpellbookPage[pages.Count];
				for (int i = 0; i < pages.Count; i++)
				{
					RecipeCatalogEntry page = pages[i];
					view[i] = new SpellbookPage
					{
						id = page.id, name = page.name, x = page.targetX, y = page.targetY,
						radius = page.radius, itemId = page.resultItemId, amount = page.amount,
					};
				}

				return view;
			}
		}

		public void RequestChest(int cellX, int cellY, int cellZ)
		{
			Chest = Look(new Numerics.Vector3Int(cellX, cellY, cellZ));
		}

		public void RequestChestPut(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Numerics.Vector3Int cell = new Numerics.Vector3Int(cellX, cellY, cellZ);
			Numerics.Vector3 standing = world.PositionOf(me.Id);

			// 가방에서 먼저 뺀다 — 넣다 남으면 도로 돌려준다(사라지는 물건은 없다).
			int missing = world.TryConsume(me.Id, itemId, amount);
			int moving = amount - missing;
			if (moving > 0)
			{
				int leftover = world.Storages.Put(cell, ItemCatalog.Find(itemId), moving, standing.x, standing.z);
				if (leftover > 0)
					world.TryGather(me.Id, ItemCatalog.Find(itemId), leftover);
			}

			Chest = Look(cell);
		}

		public void RequestChestTake(int cellX, int cellY, int cellZ, int itemId, int amount)
		{
			Numerics.Vector3Int cell = new Numerics.Vector3Int(cellX, cellY, cellZ);
			Numerics.Vector3 standing = world.PositionOf(me.Id);

			int taken = world.Storages.Take(cell, itemId, amount, standing.x, standing.z);
			if (taken > 0)
			{
				int leftover = world.TryGather(me.Id, ItemCatalog.Find(itemId), taken);
				if (leftover > 0)
					world.Storages.Put(cell, ItemCatalog.Find(itemId), leftover, standing.x, standing.z);
			}

			Chest = Look(cell);
		}

		private ChestView Look(Numerics.Vector3Int cell)
		{
			List<BagSaveEntry> contents = world.Storages.Contents(cell);
			BagEntry[] items = new BagEntry[contents.Count];
			for (int i = 0; i < contents.Count; i++)
				items[i] = new BagEntry { itemId = contents[i].itemId, amount = contents[i].amount };

			return new ChestView { x = cell.x, y = cell.y, z = cell.z, items = items };
		}

		/// <summary>내 가방 — 화면이 읽어 간다.</summary>
		public int BagCount(int itemId) => world.BagCount(me.Id, itemId);
	}
}
