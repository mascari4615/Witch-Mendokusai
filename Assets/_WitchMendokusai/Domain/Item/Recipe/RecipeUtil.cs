using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	/// <summary>
	/// 제조법 열쇠 만들기 — 규칙 본체는 판정 층(<see cref="RecipeKey"/>)에 있다 (TASK-WM-215).
	/// 여기는 게임 쪽 타입(ItemData)을 번호로 바꿔 넘기는 얇은 껍데기다.
	/// </summary>
	public class RecipeUtil
	{
		public static string RecipeToString(Recipe recipe)
		{
			return RecipeKey.Build(recipe.Type, recipe.Items.Select(ingredient => ingredient.ItemData.ID));
		}

		public static string RecipeToString(RecipeType type, List<int> ingredientIDs)
		{
			// 옛 동작 유지 — 넘겨받은 목록을 그 자리에서 정렬한다(부르는 쪽이 그걸 기대할 수 있다).
			ingredientIDs.Sort();
			return RecipeKey.Build(type, ingredientIDs);
		}
	}
}
