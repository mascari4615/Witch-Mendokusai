using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Aspect;

namespace WitchMendokusai
{
	/// <summary>
	/// 육각 종이 화면. 칸을 먼저 고르고 성질을 고르거나, 성질을 먼저 고르고 칸을 누름. 놓은 것을 누르면 지워짐
	/// </summary>
	public sealed class HexPaperView : AspectPuzzleView
	{
		private const string RULE = "그려진 성질을 전부 한 덩어리로 잇습니다. 한쪽이 다른 쪽의 재료인 것끼리 붙습니다. 놓은 것을 누르면 지워집니다.";
		private const string NO_INK = "잉크가 없습니다. 놓은 것을 눌러 지우세요.";
		private const string INK_FORMAT = "잉크 {0}";
		private const string SOLVED = "다 이음";
		private const string CLEAR = "비우기";

		private readonly List<HexPaperGame> games;
		private readonly Dictionary<HexCoord, HexCell> cells = new();
		private readonly Dictionary<HexCoord, Vector2> centers = new();
		private int gameIndex;
		private HexCoord? selectedCell;
		private int? brush;

		public HexPaperView(AspectPuzzleSettings settings, AspectPuzzleBook book, AspectLinks links, MenuFocus focus) : base(settings, links, focus)
		{
			games = book.Papers.Select(paper => new HexPaperGame(new HexPaper(paper, links))).ToList();
			BuildPuzzleList(book.Papers.Select(paper => paper.Id).ToList(), SelectPaper);
			Palette.Build(links.Aspects);
			Palette.Picked += OnPicked;
			AddAction(CLEAR, OnClear);
			BuildSide();
			SelectPaper(0);
		}

		private HexPaperGame Game => games[gameIndex];

		public override void Refresh()
		{
			Dictionary<HexCoord, int> board = Game.Board;
			HashSet<HexCoord> linked = LinkedCells(board);
			foreach (KeyValuePair<HexCoord, HexCell> pair in cells)
			{
				bool hasAspect = board.TryGetValue(pair.Key, out int aspect);
				HexCell cell = pair.Value;
				cell.ShowAspect(hasAspect ? SpriteOf(aspect) : null);
				cell.EnableInClassList(AspectCell.ANCHOR, Game.Paper.Anchors.ContainsKey(pair.Key));
				cell.EnableInClassList(AspectCell.PLACED, Game.IsPlaced(pair.Key));
				cell.EnableInClassList(AspectCell.LINKED, linked.Contains(pair.Key));
				cell.EnableInClassList(AspectCell.LOOSE, Game.IsPlaced(pair.Key) && Links.CollectGroup(pair.Key, board, coord => coord.Neighbors()).Count == 1);
				cell.EnableInClassList(AspectCell.SELECTED, selectedCell.HasValue && selectedCell.Value.Equals(pair.Key));
			}

			Palette.Show(brush, FitsTest(board), FitsAllTest(board));
			SetShown(Palette, Game.IsSolved == false);
			ShowSolved(gameIndex, Game.IsSolved);
			ShowText(Game.IsSolved ? SOLVED : string.Format(INK_FORMAT, Game.InkLeft), RULE);
			ShowLinks(LinkPairs(board));
		}

		protected override void PlaceCells()
		{
			int radius = Game.Paper.Radius;
			int across = 2 * radius + 1;
			// 뾰족한 육각형: 너비는 반지름의 2 * 0.866, 줄 사이는 반지름의 1.5
			float byWidth = BoardRect.width / (across * 2f * HexCell.WIDTH_PER_HEIGHT);
			float byHeight = BoardRect.height / (1.5f * across + 0.5f);
			float hexRadius = Mathf.Min(Settings.HexMaxRadius, byWidth, byHeight);
			float columnStep = 2f * HexCell.WIDTH_PER_HEIGHT * hexRadius;
			float height = 2f * hexRadius - Settings.HexGap;
			Vector2 size = new(height * HexCell.WIDTH_PER_HEIGHT, height);
			foreach (KeyValuePair<HexCoord, HexCell> pair in cells)
			{
				HexCoord coord = pair.Key;
				centers[coord] = BoardRect.size * 0.5f + new Vector2(columnStep * (coord.Q + coord.R * 0.5f), 1.5f * hexRadius * coord.R);
				pair.Value.Place(centers[coord], size);
			}
		}

		protected override AspectCell NextCell(AspectCell from, BoardArrow arrow)
		{
			HexCoord coord = cells.First(pair => pair.Value == from).Key;
			return BoardArrows.Hex(coord, arrow, Game.Paper.Radius).Where(cells.ContainsKey).Select(next => cells[next]).FirstOrDefault();
		}

		private void SelectPaper(int index)
		{
			gameIndex = index;
			ShowNotice(string.Empty);
			selectedCell = null;
			brush = null;
			cells.Clear();
			centers.Clear();
			ClearBoard();
			foreach (HexCoord coord in Game.Paper.Cells)
			{
				HexCell cell = new(() => OnCell(coord));
				cells.Add(coord, cell);
				AddCell(cell);
			}
			ShowCurrentPuzzle(index);
			FinishBoard();
			Refresh();
		}

		private void OnCell(HexCoord coord)
		{
			ShowNotice(string.Empty);
			Dictionary<HexCoord, int> board = Game.Board;
			if (Game.TryErase(coord))
			{
				// 지운 것으로 끝
			}
			else if (board.TryGetValue(coord, out int aspect))
			{
				ShowPartners(aspect, Links.Aspects);
			}
			else if (Game.InkLeft <= 0)
			{
				ShowNotice(NO_INK);
			}
			else if (Game.CanPlace(coord))
			{
				if (brush.HasValue)
				{
					Game.TryPlace(coord, brush.Value);
				}
				else
				{
					selectedCell = selectedCell.HasValue && selectedCell.Value.Equals(coord) ? null : coord;
				}
			}
			Refresh();
		}

		private void OnPicked(int aspect)
		{
			ShowNotice(string.Empty);
			if (selectedCell.HasValue)
			{
				Game.TryPlace(selectedCell.Value, aspect);
				selectedCell = null;
				brush = null;
			}
			else
			{
				brush = brush == aspect ? null : aspect;
			}
			Refresh();
		}

		private void OnClear()
		{
			ShowNotice(string.Empty);
			Game.Clear();
			selectedCell = null;
			brush = null;
			Refresh();
		}

		// 그려진 칸에 이어진 칸
		private HashSet<HexCoord> LinkedCells(Dictionary<HexCoord, int> board)
		{
			HashSet<HexCoord> linked = new();
			foreach (HexCoord anchor in Game.Paper.Anchors.Keys)
			{
				HashSet<HexCoord> group = Game.Paper.GroupFrom(anchor, board);
				// 그려진 칸 둘 이상이 이어진 덩어리만 이어진 것으로 보임 (번갈아와 같음)
				if (Game.Paper.Anchors.Keys.Count(group.Contains) > 1)
				{
					linked.UnionWith(group);
				}
			}
			return linked;
		}

		// 고른 칸의 이웃 하나와라도 붙는 성질, 칸 없이 고른 성질이 있으면 그것과 붙는 성질
		private System.Func<int, bool> FitsTest(Dictionary<HexCoord, int> board)
		{
			if (selectedCell.HasValue)
			{
				List<int> near = selectedCell.Value.Neighbors().Where(board.ContainsKey).Select(coord => board[coord]).ToList();
				return aspect => near.Any(other => Links.AreLinked(aspect, other));
			}
			if (brush.HasValue)
			{
				int picked = brush.Value;
				return aspect => Links.AreLinked(aspect, picked);
			}
			return null;
		}

		// 고른 칸의 이웃이 둘 이상일 때 그 모두와 붙는 성질
		private System.Func<int, bool> FitsAllTest(Dictionary<HexCoord, int> board)
		{
			if (selectedCell.HasValue == false)
			{
				return null;
			}
			List<int> near = selectedCell.Value.Neighbors().Where(board.ContainsKey).Select(coord => board[coord]).ToList();
			return aspect => near.Count > 1 && near.All(other => Links.AreLinked(aspect, other));
		}

		private IEnumerable<(Vector2 From, Vector2 To)> LinkPairs(Dictionary<HexCoord, int> board)
		{
			foreach (KeyValuePair<HexCoord, int> pair in board)
			{
				foreach (HexCoord neighbor in pair.Key.Neighbors())
				{
					// 짝마다 한 번
					bool isFirst = neighbor.Q > pair.Key.Q || (neighbor.Q == pair.Key.Q && neighbor.R > pair.Key.R);
					if (isFirst && board.TryGetValue(neighbor, out int other) && Links.AreLinked(pair.Value, other) && centers.ContainsKey(pair.Key))
					{
						yield return (centers[pair.Key], centers[neighbor]);
					}
				}
			}
		}
	}
}
