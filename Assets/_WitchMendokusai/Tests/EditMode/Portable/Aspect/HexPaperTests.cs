using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai.Tests
{
	public sealed class HexPaperTests
	{
		private const int FIRE = 0;
		private const int WATER = 1;
		private const int LIFE = 4;

		private static IEnumerable<string> PaperIds() => AspectPuzzleBookFile.BOOK.Papers.Select(paper => paper.Id);

		private static HexPaperGame NewGame(string id) => new HexPaperGame(new HexPaper(AspectPuzzleBookFile.Paper(id), AspectPuzzleBookFile.LINKS));

		[Test]
		public void 종이가_넷()
		{
			Assert.That(PaperIds().Count(), Is.EqualTo(4));
		}

		[Test]
		public void 한쪽이_다른_쪽의_재료일_때만_붙는다()
		{
			AspectLinks links = AspectPuzzleBookFile.LINKS;

			Assert.That(links.AreLinked(LIFE, FIRE), Is.True);
			Assert.That(links.AreLinked(FIRE, LIFE), Is.True, "어느 쪽에서 봐도 같음");
			Assert.That(links.AreLinked(FIRE, WATER), Is.False, "같은 것의 재료끼리는 안 붙음");
			Assert.That(links.AreLinked(LIFE, LIFE), Is.False, "자기끼리는 안 붙음");
			Assert.That(links.AreLinked(LIFE, 36), Is.False, "조합에 없는 성질은 아무와도 안 붙음");
		}

		[Test]
		public void 조합은_서로_다르고_재료는_둘이_다르다()
		{
			List<AspectRecipe> recipes = AspectPuzzleBookFile.BOOK.Recipes;

			Assert.That(recipes.Select(recipe => recipe.Result).Distinct().Count(), Is.EqualTo(recipes.Count));
			Assert.That(recipes.Select(recipe => (System.Math.Min(recipe.First, recipe.Second), System.Math.Max(recipe.First, recipe.Second))).Distinct().Count(), Is.EqualTo(recipes.Count));
			Assert.That(recipes.All(recipe => recipe.First != recipe.Second), Is.True);
		}

		[TestCaseSource(nameof(PaperIds))]
		public void 찾은_풀이로_잉크_안에_풀린다(string id)
		{
			HexPaperGame game = NewGame(id);
			Assert.That(game.IsSolved, Is.False, "놓기 전에는 안 풀림");

			foreach (HexAspect step in AspectPuzzleBookFile.Paper(id).Solution)
			{
				Assert.That(game.IsSolved, Is.False, "풀이에 남는 수 없음");
				Assert.That(game.TryPlace(step.ToCoord(), step.Aspect), Is.True);
			}

			Assert.That(game.IsSolved, Is.True);
			Assert.That(game.InkLeft, Is.GreaterThanOrEqualTo(0));
		}

		[TestCaseSource(nameof(PaperIds))]
		public void 풀이에서_하나를_빼면_안_풀린다(string id)
		{
			List<HexAspect> solution = AspectPuzzleBookFile.Paper(id).Solution;
			foreach (HexAspect skipped in solution)
			{
				HexPaperGame game = NewGame(id);
				foreach (HexAspect step in solution.Where(step => step != skipped))
				{
					game.TryPlace(step.ToCoord(), step.Aspect);
				}
				Assert.That(game.IsSolved, Is.False, $"{skipped.ToCoord()} 을 뺐는데 풀림");
			}
		}

		[Test]
		public void 찢어진_칸_그려진_칸_종이_밖에는_못_놓는다()
		{
			HexPaperGame game = NewGame("tea");

			Assert.That(game.TryPlace(new HexCoord(0, 0), LIFE), Is.False, "찢어진 칸");
			Assert.That(game.TryPlace(new HexCoord(-2, 1), LIFE), Is.False, "그려진 칸");
			Assert.That(game.TryPlace(new HexCoord(3, 0), LIFE), Is.False, "종이 밖");
			Assert.That(game.TryPlace(new HexCoord(2, 1), LIFE), Is.False, "q 와 r 은 안이지만 셋째 축이 밖");
			Assert.That(game.InkLeft, Is.EqualTo(game.Paper.Ink), "못 놓으면 잉크 그대로");
		}

		[Test]
		public void 지우면_잉크가_돌아오고_다_쓰면_못_놓는다()
		{
			HexPaperGame game = NewGame("practice");
			HexCoord first = new HexCoord(0, -1);
			HexCoord second = new HexCoord(0, 1);

			Assert.That(game.TryPlace(first, WATER), Is.True, "안 붙는 자리에도 놓을 수 있음");
			Assert.That(game.TryPlace(first, FIRE), Is.False, "놓은 칸에 또 못 놓음");
			Assert.That(game.TryPlace(second, WATER), Is.True);
			Assert.That(game.TryPlace(new HexCoord(0, 0), LIFE), Is.False, "잉크 없음");
			Assert.That(game.TryErase(first), Is.True);
			Assert.That(game.TryErase(first), Is.False, "빈칸은 지울 것 없음");
			Assert.That(game.TryErase(new HexCoord(-1, 0)), Is.False, "그려진 것은 못 지움");
			Assert.That(game.InkLeft, Is.EqualTo(1));
			Assert.That(game.TryPlace(new HexCoord(0, 0), LIFE), Is.True);
			Assert.That(game.IsSolved, Is.True);
		}

		[Test]
		public void 다_이은_종이는_지우지_못하고_비우기만_된다()
		{
			HexPaperGame game = NewGame("practice");
			game.TryPlace(new HexCoord(0, 0), LIFE);

			Assert.That(game.TryErase(new HexCoord(0, 0)), Is.False);
			Assert.That(game.TryPlace(new HexCoord(0, 1), LIFE), Is.False);
			game.Clear();
			Assert.That(game.IsSolved, Is.False);
			Assert.That(game.InkLeft, Is.EqualTo(game.Paper.Ink));
		}

		[Test]
		public void 육각_칸_수는_반지름의_식과_같다()
		{
			foreach (HexPaperData data in AspectPuzzleBookFile.BOOK.Papers)
			{
				HexPaper paper = new HexPaper(data, AspectPuzzleBookFile.LINKS);
				Assert.That(paper.Cells.Count, Is.EqualTo(3 * data.Radius * (data.Radius + 1) + 1 - data.Holes.Count), data.Id);
			}
		}
	}
}
