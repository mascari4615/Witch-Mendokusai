using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai.Tests
{
	public sealed class BoardArrowsTests
	{
		private static readonly BoardArrow[] ARROWS = { BoardArrow.Left, BoardArrow.Right, BoardArrow.Up, BoardArrow.Down };

		private static IEnumerable<string> PaperIds() => AspectPuzzleBookFile.BOOK.Papers.Select(paper => paper.Id);

		private static IEnumerable<string> BoardIds() => AspectPuzzleBookFile.BOOK.Boards.Select(board => board.Id);

		// 화면과 같은 방식. 후보 중 실제로 있는 첫 칸
		private static HashSet<TCell> Reach<TCell>(TCell from, HashSet<TCell> cells, Func<TCell, BoardArrow, IEnumerable<TCell>> path)
		{
			HashSet<TCell> reached = new HashSet<TCell> { from };
			Queue<TCell> open = new Queue<TCell>(reached);
			while (open.Count > 0)
			{
				TCell current = open.Dequeue();
				foreach (BoardArrow arrow in ARROWS)
				{
					List<TCell> next = path(current, arrow).Where(cells.Contains).Take(1).ToList();
					if (next.Count > 0 && reached.Add(next[0]))
					{
						open.Enqueue(next[0]);
					}
				}
			}
			return reached;
		}

		[TestCaseSource(nameof(PaperIds))]
		public void 종이의_모든_칸에_방향_키로_간다(string id)
		{
			HexPaper paper = new HexPaper(AspectPuzzleBookFile.Paper(id), AspectPuzzleBookFile.LINKS);
			HashSet<HexCoord> cells = new HashSet<HexCoord>(paper.Cells);
			foreach (HexCoord from in cells)
			{
				Assert.That(Reach(from, cells, (cell, arrow) => BoardArrows.Hex(cell, arrow, paper.Radius)), Is.EquivalentTo(cells), $"{from} 에서");
			}
		}

		[TestCaseSource(nameof(BoardIds))]
		public void 번갈아의_모든_칸에_방향_키로_간다(string id)
		{
			TurnBoard board = new TurnBoard(AspectPuzzleBookFile.Board(id), AspectPuzzleBookFile.LINKS);
			HashSet<int> cells = new HashSet<int>(Enumerable.Range(0, TurnGrid.CELL_COUNT).Where(cell => board.IsHole(cell) == false));
			foreach (int from in cells)
			{
				Assert.That(Reach(from, cells, BoardArrows.Diamond), Is.EquivalentTo(cells), $"{from} 에서");
			}
		}

		[Test]
		public void 아래로_갔다_위로_오면_제자리()
		{
			Assert.That(BoardArrows.Diamond(12, BoardArrow.Down).First(), Is.EqualTo(13));
			Assert.That(BoardArrows.Diamond(13, BoardArrow.Up).First(), Is.EqualTo(12));
			HexCoord below = BoardArrows.Hex(new HexCoord(0, 0), BoardArrow.Down, 2).First();
			Assert.That(BoardArrows.Hex(below, BoardArrow.Up, 2).First(), Is.EqualTo(new HexCoord(0, 0)));
		}
	}
}
