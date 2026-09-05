using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>제작 한 줄에 드는 재료 하나.</summary>
	[Serializable]
	public class CraftIngredientEntry
	{
		public int itemId;
		public int amount = 1;
	}

	/// <summary>
	/// 제작 한 줄 — 「이것들을 넣으면 이게 나온다」 (TASK-WM-217).
	/// <c>percentage</c> 100 = 반드시 성공. 0 이하도 성공으로 본다(안 적힌 옛 자료를 실패로 만들지 않는다).
	/// </summary>
	[Serializable]
	public class CraftRecipeEntry
	{
		public int id;
		public string name = string.Empty;
		public int resultItemId;
		public int resultAmount = 1;
		public float percentage = 100f;
		public CraftIngredientEntry[] items = Array.Empty<CraftIngredientEntry>();
	}

	/// <summary>세계가 아는 제작표 (아이템·건물 목록과 같은 모양 — 정본은 게임 자산).</summary>
	[Serializable]
	public class CraftCatalogData
	{
		public CraftRecipeEntry[] recipes = Array.Empty<CraftRecipeEntry>();
	}

	/// <summary>제작 한 판의 결과 — 됐나, 무엇이 몇 개 나왔나.</summary>
	public struct CraftResult
	{
		/// <summary>재료가 있고 세계가 아는 줄이라 <b>시도했다</b>(성공했다는 뜻이 아니다).</summary>
		public bool Attempted;

		/// <summary>주사위를 이겼나.</summary>
		public bool Succeeded;

		public int RecipeId;
		public int ResultItemId;
		public int ResultAmount;

		/// <summary>왜 못 했나 — 사람에게 그대로 보여 준다(조용히 실패하면 「고장」으로 읽힌다).</summary>
		public string Denied;
	}

	/// <summary>
	/// 제작을 <b>세계가 판정한다</b> (TASK-WM-217).
	///
	/// ★ 왜: 지금 제작은 창 안에서 끝난다 — 재료 확인도, <b>성공 주사위도</b>, 지급도 창이 한다.
	///   창을 고친 사람은 언제나 성공하고 무엇이든 만든다. 그건 판정이 아니라 신고다.
	///   그리고 게임 창과 웹 창이 각자 굴리면 같은 재료로 서로 다른 결과가 나온다(같은 세계가 아니다).
	///
	/// 주사위는 <b>밖에서 넣는다</b>(0~100). 그래야 시험이 「성공한 판」과 「실패한 판」을 모두 잴 수 있고,
	/// 서버가 무엇으로 굴리는지도 한 자리에서 바뀐다.
	/// </summary>
	public sealed class WorldCraftBook
	{
		private readonly List<CraftRecipeEntry> recipes = new List<CraftRecipeEntry>();
		private readonly Dictionary<int, CraftRecipeEntry> byId = new Dictionary<int, CraftRecipeEntry>();

		public WorldCraftBook(CraftCatalogData data)
		{
			if (data?.recipes == null)
				return;

			for (int i = 0; i < data.recipes.Length; i++)
			{
				CraftRecipeEntry recipe = data.recipes[i];
				if (recipe == null || byId.ContainsKey(recipe.id))
					continue;

				recipes.Add(recipe);
				byId[recipe.id] = recipe;
			}
		}

		public IReadOnlyList<CraftRecipeEntry> Recipes => recipes;

		public CraftRecipeEntry Find(int recipeId)
		{
			return byId.TryGetValue(recipeId, out CraftRecipeEntry recipe) ? recipe : null;
		}

		/// <summary>
		/// 그 줄대로 만들 수 있나 — <b>재료만</b> 본다(주사위는 나중이다).
		/// <paramref name="carrying"/> 은 「그 아이템을 몇 개 들고 있나」.
		/// </summary>
		public bool CanMake(int recipeId, Func<int, int> carrying, out string denied)
		{
			denied = null;
			CraftRecipeEntry recipe = Find(recipeId);
			if (recipe == null)
			{
				denied = "세계가 모르는 제작이다";
				return false;
			}

			if (recipe.items == null)
				return true;

			for (int i = 0; i < recipe.items.Length; i++)
			{
				CraftIngredientEntry need = recipe.items[i];
				if (need == null || need.amount <= 0)
					continue;

				// ⚠ 번호 0(나무)도 진짜 재료다 — 「없음」으로 거르면 나무로 만드는 것이 공짜가 된다.
				int have = carrying == null ? 0 : carrying(need.itemId);
				if (have >= need.amount)
					continue;

				denied = "재료가 모자란다";
				return false;
			}

			return true;
		}

		/// <summary>
		/// 판정한다. <paramref name="roll"/> = 0~100 사이 주사위(서버가 굴려 넣는다).
		///
		/// 재료를 실제로 빼고 넣는 것은 <b>부르는 쪽</b> 몫이다 — 가방 규칙은 세계의 다른 자리에 있다.
		/// 여기서 정하는 것은 「됐나 · 무엇이 몇 개인가」뿐이다.
		/// </summary>
		public CraftResult Judge(int recipeId, Func<int, int> carrying, float roll)
		{
			CraftResult result = new CraftResult { RecipeId = recipeId };

			if (CanMake(recipeId, carrying, out string denied) == false)
			{
				result.Denied = denied;
				return result;
			}

			CraftRecipeEntry recipe = Find(recipeId);
			result.Attempted = true;

			// 안 적힌 옛 자료(0 이하)를 실패로 만들지 않는다 — 그러면 자료를 안 고친 줄이 전부 죽는다.
			float chance = recipe.percentage <= 0f ? 100f : recipe.percentage;
			result.Succeeded = roll < chance;

			if (result.Succeeded == false)
				return result;

			result.ResultItemId = recipe.resultItemId;
			result.ResultAmount = recipe.resultAmount <= 0 ? 1 : recipe.resultAmount;
			return result;
		}
	}
}
