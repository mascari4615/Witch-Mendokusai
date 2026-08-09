using System;
using System.Collections.Generic;
using WitchMendokusai.Net;

namespace WitchMendokusai
{
	/// <summary>
	/// 게임의 제작 화면을 <b>세계에 잇는 줄</b> (TASK-WM-217).
	///
	/// 게임은 「제작 구멍」으로만 말하고, 이 줄이 그 구멍을 채운다 — 그래서 통로가 갈려도
	/// (내 안의 세계 / 멀리 있는 세계) 게임 코드는 안 바뀐다.
	/// </summary>
	public sealed class WorldLinkCraftChannel : IWorldCraftChannel
	{
		private readonly IWorldLink link;

		// 제작표는 들어올 때 한 번 오고 안 바뀐다 — 매번 새로 짓지 않는다.
		private CraftBookEntryView[] lastBook;
		private CraftRecipeEntry[] bookCache = Array.Empty<CraftRecipeEntry>();

		public WorldLinkCraftChannel(IWorldLink link)
		{
			this.link = link;
		}

		public bool IsActive => link != null;

		public IReadOnlyList<CraftRecipeEntry> Recipes
		{
			get
			{
				CraftBookEntryView[] book = link?.CraftBook;
				if (book == null)
					return Array.Empty<CraftRecipeEntry>();

				if (ReferenceEquals(book, lastBook))
					return bookCache;

				CraftRecipeEntry[] recipes = new CraftRecipeEntry[book.Length];
				for (int i = 0; i < book.Length; i++)
				{
					CraftBookEntryView view = book[i];
					int count = view.itemIds == null ? 0 : view.itemIds.Length;
					CraftIngredientEntry[] items = new CraftIngredientEntry[count];
					for (int need = 0; need < count; need++)
					{
						items[need] = new CraftIngredientEntry
						{
							itemId = view.itemIds[need],
							amount = view.amounts != null && need < view.amounts.Length ? view.amounts[need] : 1,
						};
					}

					recipes[i] = new CraftRecipeEntry
					{
						id = view.recipeId, name = view.name,
						resultItemId = view.resultItemId, resultAmount = view.resultAmount,
						percentage = view.percentage, items = items,
					};
				}

				lastBook = book;
				bookCache = recipes;
				return bookCache;
			}
		}

		public void Request(int recipeId) => link?.RequestCraft(recipeId);

		public bool TryTakeResult(out CraftResult result)
		{
			result = default;
			CraftedMessage said = link?.TakeCraftResult();
			if (said == null)
				return false;

			result = new CraftResult
			{
				RecipeId = said.recipeId,
				Attempted = said.attempted,
				Succeeded = said.succeeded,
				ResultItemId = said.itemId,
				ResultAmount = said.amount,
				Denied = said.denied,
			};

			return true;
		}
	}
}
