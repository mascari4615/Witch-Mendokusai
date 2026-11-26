using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai.Tests
{
	public sealed class TurnBoardTests
	{
		private const int FIRE = 0;
		private const int WATER = 1;
		private const int LIFE = 4;
		private const int EARTH = 5;
		private const int NO_LIMIT = 99;
		private const int CENTER = 12;

		private static IEnumerable<TestCaseData> Runs()
		{
			foreach (TurnBoardData board in AspectPuzzleBookFile.BOOK.Boards)
			{
				foreach (TurnRun run in board.Runs)
				{
					yield return new TestCaseData(board.Id, run.Habit);
				}
			}
		}

		// 가운데 (12) 주위: 왼쪽 위 11, 오른쪽 위 7, 오른쪽 아래 13, 왼쪽 아래 17
		private static TurnBoardData EmptyAround(params int[] holes) => new TurnBoardData
		{
			Id = "test",
			Anchors = new List<TurnCell> { new TurnCell { Cell = 0, Aspect = FIRE }, new TurnCell { Cell = 24, Aspect = EARTH } },
			Holes = holes.ToList(),
			Pool = new List<int> { FIRE, LIFE },
			Queue = new List<int> { WATER, EARTH },
		};

		private static int DollCell(TurnBoardData data, TurnHabitType habit)
		{
			TurnBoard board = new TurnBoard(data, AspectPuzzleBookFile.LINKS);
			List<TurnMove> added = board.Play(new List<TurnMove>(), CENTER, LIFE, TurnHabits.Get(habit));
			return added.Count == 2 ? added[1].Cell : -1;
		}

		[Test]
		public void 문제와_버릇이_스물여섯_조합()
		{
			Assert.That(Runs().Count(), Is.EqualTo(26));
		}

		[TestCaseSource(nameof(Runs))]
		public void 찾은_풀이로_제한_안에_풀린다(string boardId, TurnHabitType habit)
		{
			TurnBoardData data = AspectPuzzleBookFile.Board(boardId);
			TurnRun run = data.Runs.Single(candidate => candidate.Habit == habit);
			TurnGame game = AspectPuzzleBookFile.NewGame(data, habit, run.Limit);
			Assert.That(game.IsSolved, Is.False);

			foreach (TurnCell step in run.Solution)
			{
				Assert.That(game.IsSolved, Is.False, "풀이에 남는 수 없음");
				Assert.That(game.TryPut(step.Cell, step.Aspect), Is.True);
			}

			Assert.That(game.IsSolved, Is.True);
			Assert.That(game.MovesLeft, Is.GreaterThanOrEqualTo(0));
		}

		[Test]
		public void 욘의_수로_풀리면_인형은_안_놓는다()
		{
			TurnBoardData data = EmptyAround();
			data.Anchors[1] = new TurnCell { Cell = 2, Aspect = WATER };
			TurnGame game = AspectPuzzleBookFile.NewGame(data, TurnHabitType.ClockwiseNear, 1);

			Assert.That(game.TryPut(1, LIFE), Is.True);
			Assert.That(game.IsSolved, Is.True);
			Assert.That(game.Moves.Count, Is.EqualTo(1));
		}

		[TestCaseSource(nameof(Runs))]
		public void 연습_말고는_다른_버릇의_풀이가_안_통한다(string boardId, TurnHabitType habit)
		{
			if (boardId == "practice")
			{
				Assert.Ignore("연습은 같은 풀이를 허락");
			}
			TurnBoardData data = AspectPuzzleBookFile.Board(boardId);
			TurnRun run = data.Runs.Single(candidate => candidate.Habit == habit);
			foreach (TurnRun other in data.Runs.Where(candidate => candidate.Habit != habit))
			{
				TurnGame game = AspectPuzzleBookFile.NewGame(data, other.Habit, NO_LIMIT);
				bool isLegal = run.Solution.All(step => game.IsSolved || game.TryPut(step.Cell, step.Aspect));
				Assert.That(isLegal && game.IsSolved, Is.False, $"{other.Habit} 에게도 통함");
			}
		}

		[TestCaseSource(nameof(Runs))]
		public void 무르면_한_수_전의_판과_같다(string boardId, TurnHabitType habit)
		{
			TurnBoardData data = AspectPuzzleBookFile.Board(boardId);
			TurnRun run = data.Runs.Single(candidate => candidate.Habit == habit);
			TurnGame game = AspectPuzzleBookFile.NewGame(data, habit, run.Limit);
			List<Dictionary<int, int>> history = new List<Dictionary<int, int>>();
			foreach (TurnCell step in run.Solution)
			{
				history.Add(game.Cells);
				game.TryPut(step.Cell, step.Aspect);
			}
			for (int i = history.Count - 1; i >= 0; i--)
			{
				Assert.That(game.TryUndo(), Is.True);
				Assert.That(game.Cells, Is.EquivalentTo(history[i]));
			}
			Assert.That(game.TryUndo(), Is.False, "더 무를 것 없음");
			Assert.That(game.MovesLeft, Is.EqualTo(run.Limit));
		}

		[Test]
		public void 옆_칸_버릇은_왼쪽_위부터_시계_방향()
		{
			Assert.That(DollCell(EmptyAround(), TurnHabitType.ClockwiseNear), Is.EqualTo(11));
			Assert.That(DollCell(EmptyAround(11), TurnHabitType.ClockwiseNear), Is.EqualTo(7));
			Assert.That(DollCell(EmptyAround(11, 7), TurnHabitType.ClockwiseNear), Is.EqualTo(13));
			Assert.That(DollCell(EmptyAround(11, 7, 13), TurnHabitType.ClockwiseNear), Is.EqualTo(17));
		}

		[Test]
		public void 건너뛰는_버릇은_한_칸_사이를_둔다()
		{
			Assert.That(DollCell(EmptyAround(), TurnHabitType.ClockwiseSkip), Is.EqualTo(10));
			Assert.That(DollCell(EmptyAround(10), TurnHabitType.ClockwiseSkip), Is.EqualTo(2));
			Assert.That(DollCell(EmptyAround(10, 2), TurnHabitType.ClockwiseSkip), Is.EqualTo(14));
			Assert.That(DollCell(EmptyAround(10, 2, 14), TurnHabitType.ClockwiseSkip), Is.EqualTo(22));
		}

		[Test]
		public void 다가가는_버릇은_가장_가까운_빈칸_같으면_시계_방향_먼저()
		{
			Assert.That(DollCell(EmptyAround(), TurnHabitType.TowardAnchor), Is.EqualTo(11), "0 과 24 까지 거리가 같으면 왼쪽 위");
			Assert.That(DollCell(EmptyAround(11, 7), TurnHabitType.TowardAnchor), Is.EqualTo(13));
		}

		[Test]
		public void 놓을_빈칸이_없으면_인형은_쉰다()
		{
			Assert.That(DollCell(EmptyAround(11, 7, 13, 17), TurnHabitType.ClockwiseNear), Is.EqualTo(-1));
			Assert.That(DollCell(EmptyAround(11, 7, 13, 17), TurnHabitType.TowardAnchor), Is.EqualTo(-1));
			Assert.That(DollCell(EmptyAround(10, 2, 14, 22), TurnHabitType.ClockwiseSkip), Is.EqualTo(-1));
		}

		[Test]
		public void 욘은_이웃과_붙는_빈칸에_고를_수_있는_성질만()
		{
			TurnGame game = AspectPuzzleBookFile.NewGame(EmptyAround(5), TurnHabitType.ClockwiseNear, 2);

			Assert.That(game.CanPut(1, LIFE), Is.True, "불 옆의 생명");
			Assert.That(game.CanPut(1, FIRE), Is.False, "불 옆의 불은 안 붙음");
			Assert.That(game.CanPut(CENTER, LIFE), Is.False, "이웃이 없음");
			Assert.That(game.CanPut(5, LIFE), Is.False, "찢어진 칸");
			Assert.That(game.CanPut(0, LIFE), Is.False, "그려진 칸");
			Assert.That(game.CanPut(-1, LIFE), Is.False, "판 밖");
			Assert.That(game.CanPut(1, 2), Is.False, "고를 수 없는 성질");
		}

		[Test]
		public void 제한이_끝나면_못_놓고_인형의_성질은_차례로_돈다()
		{
			TurnGame game = AspectPuzzleBookFile.NewGame(EmptyAround(), TurnHabitType.ClockwiseSkip, 1);
			Assert.That(game.DollAspectAhead(0), Is.EqualTo(WATER));
			Assert.That(game.DollAspectAhead(2), Is.EqualTo(WATER), "두 개짜리 차례가 한 바퀴");
			Assert.That(game.TryPut(1, LIFE), Is.True);
			Assert.That(game.Moves[1].Aspect, Is.EqualTo(WATER));
			Assert.That(game.DollAspectAhead(0), Is.EqualTo(EARTH));
			Assert.That(game.MovesLeft, Is.EqualTo(0));
			Assert.That(game.CanPut(5, LIFE), Is.False);
		}

		[Test]
		public void 미리_보기는_판을_바꾸지_않고_실제_수와_같다()
		{
			TurnGame game = AspectPuzzleBookFile.NewGame(EmptyAround(), TurnHabitType.ClockwiseNear, 3);
			List<TurnMove> preview = game.Preview(1, LIFE).ToList();
			Assert.That(game.Moves, Is.Empty);
			game.TryPut(1, LIFE);
			Assert.That(game.Moves, Is.EqualTo(preview));
		}

		[Test]
		public void 판의_가장자리에서_반대쪽으로_넘어가지_않는다()
		{
			Assert.That(TurnGrid.Neighbors(4), Is.EquivalentTo(new[] { 3, 9 }), "오른쪽 꼭짓점");
			Assert.That(TurnGrid.Neighbors(20), Is.EquivalentTo(new[] { 15, 21 }), "왼쪽 꼭짓점");
			Assert.That(TurnGrid.Neighbors(5), Is.EquivalentTo(new[] { 0, 6, 10 }));
		}
	}
}
