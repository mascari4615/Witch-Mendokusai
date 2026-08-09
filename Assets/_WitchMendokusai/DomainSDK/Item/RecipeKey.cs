using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 「이 재료 묶음이 어떤 제조법인가」를 가리키는 열쇠 (TASK-WM-215).
	///
	/// 재료를 넣은 <b>순서는 상관없다</b> — 같은 재료면 같은 열쇠가 나와야 제조법이 걸린다.
	/// 그래서 재료 번호를 정렬해 이어 붙인다. 이 규칙이 서버와 게임에서 갈리면
	/// 같은 재료를 넣고도 한쪽에서만 물건이 나온다.
	/// </summary>
	public static class RecipeKey
	{
		public const char SEPARATOR = ',';

		/// <summary>주어진 목록을 <b>정렬해서</b> 열쇠를 만든다(원본 목록은 건드리지 않는다).</summary>
		public static string Build(RecipeType type, IEnumerable<int> ingredientIds)
		{
			List<int> sorted = new List<int>();
			if (ingredientIds != null)
				sorted.AddRange(ingredientIds);

			sorted.Sort();

			return type + SEPARATOR.ToString() + string.Join(SEPARATOR.ToString(), sorted);
		}
	}
}
