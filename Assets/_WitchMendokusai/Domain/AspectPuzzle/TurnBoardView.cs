using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 번갈아 화면. 칸과 성질을 고르면 욘과 인형의 다음 수가 흐리게 보임. 같은 것을 한 번 더 누르거나 확정 버튼
	/// </summary>
	public sealed class TurnBoardView : AspectPuzzleView
	{
		private const string RULE = "그려진 성질을 전부 한 덩어리로 잇습니다. 욘이 하나, 인형이 하나 놓습니다. 욘은 이웃과 붙는 빈칸에만, 인형은 안 붙어도 놓고 빈칸이 없으면 쉽니다.";
		private const string HELP_FORMAT = "{0}\n{1}: {2}";
		private const string NOT_FIT = "이 자리의 이웃과 붙지 않습니다.";
		private const string DOLL_RESTS_FORMAT = "{0}: 놓을 빈칸이 없어 쉽니다.";
		private const string MOVES_FORMAT = "남은 수 {0}";
		private const string SOLVED = "다 이음";
		private const string NO_MOVES = "남은 수 없음";
		private const string PUT = "놓는다";
		private const string UNDO = "무르기";
		private const string CLEAR = "비우기";
		// 아직 인형이 없는 버릇의 자리 이름. 인형 이름은 정본에 있는 것만 씀
		private const string NO_DOLL_FORMAT = "인형 {0} (미정)";
		private const string DOLL_CURRENT = "wm-aspect-tab--current";

		// 버릇 설명. 버릇과 글 모두 임시 (2026-11-25)
		private static readonly Dictionary<TurnHabitType, string> HABIT_TEXT = new()
		{
			{ TurnHabitType.ClockwiseNear, "방금 놓은 칸의 왼쪽 위부터 시계 방향으로 첫 빈칸" },
			{ TurnHabitType.TowardAnchor, "방금 놓은 칸의 옆 중 남은 보라 칸에 가장 가까운 빈칸" },
			{ TurnHabitType.ClockwiseSkip, "한 칸을 사이에 둔 자리 중 왼쪽 위부터 시계 방향으로 첫 빈칸" },
		};

		private readonly AspectPuzzleBook book;
		private readonly Dictionary<(int Board, TurnHabitType Habit), TurnGame> games = new();
		private readonly Dictionary<int, DiamondCell> cells = new();
		private readonly Dictionary<int, Vector2> centers = new();
		private readonly Dictionary<TurnHabitType, Button> dollButtons = new();
		private readonly VisualElement dollRow;
		private readonly VisualElement queueRow;
		private readonly Button putButton;
		private int boardIndex;
		private TurnDoll doll;
		private int? selectedCell;
		private int? piece;

		public TurnBoardView(AspectPuzzleSettings settings, AspectPuzzleBook book, AspectLinks links, MenuFocus focus) : base(settings, links, focus)
		{
			this.book = book;
			doll = book.Dolls[0];
			for (int i = 0; i < book.Boards.Count; i++)
			{
				TurnBoard board = new(book.Boards[i], links);
				foreach (TurnRun run in book.Boards[i].Runs)
				{
					games.Add((i, run.Habit), new TurnGame(board, TurnHabits.Get(run.Habit), run.Limit));
				}
			}

			BuildPuzzleList(book.Boards.Select(board => board.Id).ToList(), SelectBoard);
			dollRow = Part(Side, "wm-aspect-dolls");
			queueRow = Part(Side, "wm-aspect-queue");
			BuildDolls();
			Palette.Picked += OnPicked;
			putButton = AddAction(PUT, Put);
			AddAction(UNDO, OnUndo);
			AddAction(CLEAR, OnClear);
			BuildSide();
			SelectBoard(0);
		}

		private TurnGame Game => games[(boardIndex, doll.Habit)];

		private IReadOnlyList<TurnMove> Preview => selectedCell.HasValue && piece.HasValue ? Game.Preview(selectedCell.Value, piece.Value) : System.Array.Empty<TurnMove>();

		public override void Refresh()
		{
			Dictionary<int, int> board = Game.Cells;
			IReadOnlyList<TurnMove> preview = Preview;
			Dictionary<int, int> shown = new(board);
			HashSet<int> dollCells = new(Game.Moves.Concat(preview).Where(move => move.IsDoll).Select(move => move.Cell));
			foreach (TurnMove move in preview)
			{
				shown.Add(move.Cell, move.Aspect);
			}

			HashSet<int> linked = LinkedCells(board);
			Sprite dollSprite = DollSprite(doll);
			foreach (KeyValuePair<int, DiamondCell> pair in cells)
			{
				bool hasAspect = shown.TryGetValue(pair.Key, out int aspect);
				DiamondCell cell = pair.Value;
				cell.ShowAspect(hasAspect ? SpriteOf(aspect) : null);
				cell.ShowDoll(dollCells.Contains(pair.Key), dollSprite);
				cell.EnableInClassList(AspectCell.ANCHOR, Game.Board.Anchors.ContainsKey(pair.Key));
				cell.EnableInClassList(AspectCell.PLACED, board.ContainsKey(pair.Key) && Game.Board.Anchors.ContainsKey(pair.Key) == false);
				cell.EnableInClassList(AspectCell.LINKED, linked.Contains(pair.Key));
				cell.EnableInClassList(AspectCell.SELECTED, selectedCell == pair.Key);
				cell.EnableInClassList(AspectCell.GHOST, hasAspect && board.ContainsKey(pair.Key) == false);
			}

			Palette.Show(piece, selectedCell.HasValue ? aspect => Game.CanPut(selectedCell.Value, aspect) : null);
			SetShown(Palette, Game.IsSolved == false);
			// 숨기면 누른 직후 포커스를 잃음
			putButton.EnableInClassList("wm-aspect-action--idle", preview.Count == 0);
			ShowSolved(boardIndex, book.Boards[boardIndex].Runs.Any(run => games[(boardIndex, run.Habit)].IsSolved));
			if (preview.Count == 1 && Links.IsOneGroup(Game.Board.Anchors.Keys, shown, TurnGrid.Neighbors) == false)
			{
				ShowNotice(string.Format(DOLL_RESTS_FORMAT, DollName(doll)));
			}
			ShowText(Game.IsSolved ? SOLVED : Game.MovesLeft > 0 ? string.Format(MOVES_FORMAT, Game.MovesLeft) : NO_MOVES, string.Format(HELP_FORMAT, RULE, DollName(doll), HABIT_TEXT[doll.Habit]));
			ShowDolls();
			ShowQueue();
			ShowLinks(LinkPairs(shown));
		}

		protected override void PlaceCells()
		{
			// 마름모 다섯 줄을 돌린 판. 가운데에서 꼭짓점 칸까지 네 칸, 그 칸의 반지름까지 다섯
			float radius = Mathf.Min(Settings.DiamondMaxRadius, BoardRect.width / (2f * TurnGrid.SIZE), BoardRect.height / (2f * TurnGrid.SIZE));
			Vector2 size = Vector2.one * (2f * radius * Settings.DiamondFill);
			foreach (KeyValuePair<int, DiamondCell> pair in cells)
			{
				int x = TurnGrid.X(pair.Key);
				int y = TurnGrid.Y(pair.Key);
				centers[pair.Key] = BoardRect.size * 0.5f + new Vector2(x - y, x + y - (TurnGrid.SIZE - 1)) * radius;
				pair.Value.Place(centers[pair.Key], size);
			}
		}

		protected override AspectCell NextCell(AspectCell from, BoardArrow arrow)
		{
			int cell = cells.First(pair => pair.Value == from).Key;
			return BoardArrows.Diamond(cell, arrow).Where(cells.ContainsKey).Select(next => cells[next]).FirstOrDefault();
		}

		private void SelectBoard(int index)
		{
			boardIndex = index;
			ShowNotice(string.Empty);
			if (games.ContainsKey((index, doll.Habit)) == false)
			{
				doll = book.Dolls.First(candidate => games.ContainsKey((index, candidate.Habit)));
			}
			selectedCell = null;
			piece = null;
			cells.Clear();
			centers.Clear();
			ClearBoard();
			for (int i = 0; i < TurnGrid.CELL_COUNT; i++)
			{
				int cellIndex = i;
				if (Game.Board.IsHole(cellIndex))
				{
					continue;
				}
				DiamondCell cell = new(() => OnCell(cellIndex));
				cells.Add(cellIndex, cell);
				AddCell(cell);
			}
			Palette.Build(Game.Board.Pool);
			ShowCurrentPuzzle(index);
			FinishBoard();
			Refresh();
		}

		private void SelectDoll(TurnDoll next)
		{
			doll = next;
			ShowNotice(string.Empty);
			selectedCell = null;
			piece = null;
			Refresh();
		}

		private void OnCell(int cell)
		{
			ShowNotice(string.Empty);
			if (Game.Cells.TryGetValue(cell, out int aspect))
			{
				ShowPartners(aspect, Game.Board.Pool);
			}
			else if (Game.IsSolved == false)
			{
				if (selectedCell == cell && Preview.Count > 0)
				{
					Put();
					return;
				}
				selectedCell = selectedCell == cell ? null : cell;
				if (selectedCell.HasValue && piece.HasValue && Preview.Count == 0)
				{
					piece = null;
				}
			}
			Refresh();
		}

		private void OnPicked(int aspect)
		{
			ShowNotice(string.Empty);
			if (piece == aspect && Preview.Count > 0)
			{
				Put();
				return;
			}
			piece = piece == aspect ? null : aspect;
			if (selectedCell.HasValue && piece.HasValue && Preview.Count == 0)
			{
				piece = null;
				ShowNotice(NOT_FIT);
			}
			Refresh();
		}

		private void Put()
		{
			ShowNotice(string.Empty);
			if (selectedCell.HasValue && piece.HasValue && Game.TryPut(selectedCell.Value, piece.Value))
			{
				selectedCell = null;
				piece = null;
			}
			Refresh();
		}

		private void OnUndo()
		{
			ShowNotice(string.Empty);
			Game.TryUndo();
			piece = null;
			selectedCell = null;
			Refresh();
		}

		private void OnClear()
		{
			ShowNotice(string.Empty);
			Game.Clear();
			selectedCell = null;
			piece = null;
			Refresh();
		}

		private void BuildDolls()
		{
			foreach (TurnDoll candidate in book.Dolls)
			{
				Button button = new(() => SelectDoll(candidate)) { text = DollName(candidate) };
				button.AddToClassList("wm-mb-tab");
				Focus.Register(button);
				dollButtons.Add(candidate.Habit, button);
				dollRow.Add(button);
			}
		}

		private void ShowDolls()
		{
			foreach (KeyValuePair<TurnHabitType, Button> pair in dollButtons)
			{
				SetShown(pair.Value, games.ContainsKey((boardIndex, pair.Key)));
				pair.Value.EnableInClassList(DOLL_CURRENT, pair.Key == doll.Habit);
			}
		}

		// 인형이 이어서 놓을 성질. 앞의 것이 이번
		private void ShowQueue()
		{
			queueRow.Clear();
			for (int i = 0; i < Settings.DollQueueShown; i++)
			{
				VisualElement icon = Part(queueRow, "wm-aspect-queue__icon");
				icon.EnableInClassList("wm-aspect-queue__icon--now", i == 0);
				icon.style.backgroundImage = new StyleBackground(SpriteOf(Game.DollAspectAhead(i)));
			}
		}

		private HashSet<int> LinkedCells(Dictionary<int, int> board)
		{
			HashSet<int> linked = new();
			foreach (int anchor in Game.Board.Anchors.Keys)
			{
				HashSet<int> group = Game.Board.GroupFrom(anchor, board);
				// 그려진 칸 둘 이상이 이어진 덩어리만 이어진 것으로 보임
				if (Game.Board.Anchors.Keys.Count(group.Contains) > 1)
				{
					linked.UnionWith(group);
				}
			}
			return linked;
		}

		private IEnumerable<(Vector2 From, Vector2 To)> LinkPairs(Dictionary<int, int> board)
		{
			foreach (KeyValuePair<int, int> pair in board)
			{
				foreach (int neighbor in TurnGrid.Neighbors(pair.Key))
				{
					if (neighbor > pair.Key && board.TryGetValue(neighbor, out int other) && Links.AreLinked(pair.Value, other) && centers.ContainsKey(pair.Key))
					{
						yield return (centers[pair.Key], centers[neighbor]);
					}
				}
			}
		}

		private string DollName(TurnDoll candidate) => candidate.DollId >= 0 ? SOHelper.GetDoll(candidate.DollId).Name : string.Format(NO_DOLL_FORMAT, book.Dolls.IndexOf(candidate) + 1);

		private static Sprite DollSprite(TurnDoll candidate) => candidate.DollId >= 0 ? SOHelper.GetDoll(candidate.DollId).Sprite : null;
	}
}
