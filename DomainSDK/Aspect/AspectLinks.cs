using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>
	/// 성질끼리 붙는 관계. 한쪽이 다른 쪽의 재료일 때만 붙음.
	/// 육각 종이와 번갈아가 같은 관계와 같은 덩어리 찾기를 씀
	/// </summary>
	public sealed class AspectLinks
	{
		private readonly Dictionary<int, HashSet<int>> partners = new Dictionary<int, HashSet<int>>();
		private readonly List<int> aspects;

		public AspectLinks(IEnumerable<AspectRecipe> recipes)
		{
			foreach (AspectRecipe recipe in recipes)
			{
				Connect(recipe.Result, recipe.First);
				Connect(recipe.Result, recipe.Second);
			}
			aspects = new List<int>(partners.Keys);
			aspects.Sort();
		}

		/// <summary>조합에 한 번이라도 나오는 성질. 번호 순서</summary>
		public IReadOnlyList<int> Aspects => aspects;

		public bool AreLinked(int first, int second) => partners.TryGetValue(first, out HashSet<int> found) && found.Contains(second);

		/// <summary>from 에서 붙은 칸만 따라가 닿는 한 덩어리. 칸의 모양 (육각, 마름모) 은 neighbors 만 앎</summary>
		public HashSet<TCell> CollectGroup<TCell>(TCell from, IReadOnlyDictionary<TCell, int> board, Func<TCell, IEnumerable<TCell>> neighbors)
		{
			HashSet<TCell> group = new HashSet<TCell> { from };
			Stack<TCell> open = new Stack<TCell>();
			open.Push(from);
			while (open.Count > 0)
			{
				TCell current = open.Pop();
				foreach (TCell neighbor in neighbors(current))
				{
					if (board.TryGetValue(neighbor, out int aspect) && AreLinked(board[current], aspect) && group.Add(neighbor))
					{
						open.Push(neighbor);
					}
				}
			}
			return group;
		}

		/// <summary>anchors 가 전부 한 덩어리인지</summary>
		public bool IsOneGroup<TCell>(IEnumerable<TCell> anchors, IReadOnlyDictionary<TCell, int> board, Func<TCell, IEnumerable<TCell>> neighbors)
		{
			HashSet<TCell> group = null;
			foreach (TCell anchor in anchors)
			{
				group ??= CollectGroup(anchor, board, neighbors);
				if (group.Contains(anchor) == false)
				{
					return false;
				}
			}
			return true;
		}

		private void Connect(int result, int ingredient)
		{
			PartnersOf(result).Add(ingredient);
			PartnersOf(ingredient).Add(result);
		}

		private HashSet<int> PartnersOf(int aspect)
		{
			if (partners.TryGetValue(aspect, out HashSet<int> found) == false)
			{
				found = new HashSet<int>();
				partners.Add(aspect, found);
			}
			return found;
		}
	}
}
