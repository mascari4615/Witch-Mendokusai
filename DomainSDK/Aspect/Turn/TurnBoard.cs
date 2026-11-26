using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>판에 놓인 한 수</summary>
	public readonly struct TurnMove
	{
		public readonly int Cell;
		public readonly int Aspect;
		public readonly bool IsDoll;

		public TurnMove(int cell, int aspect, bool isDoll)
		{
			Cell = cell;
			Aspect = aspect;
			IsDoll = isDoll;
		}
	}

	/// <summary>
	/// 번갈아 판의 규칙. 욘이 하나, 인형이 하나 놓아 그려진 성질을 전부 한 덩어리로.
	/// 놓은 수는 모름. 수의 목록을 받아 판을 매번 다시 세움 (무르기가 목록 자르기로 끝남)
	/// </summary>
	public sealed class TurnBoard
	{
		private readonly AspectLinks links;
		private readonly HashSet<int> holes;
		private readonly Dictionary<int, int> anchors = new Dictionary<int, int>();

		public TurnBoard(TurnBoardData data, AspectLinks links)
		{
			this.links = links;
			Pool = data.Pool;
			Queue = data.Queue;
			holes = new HashSet<int>(data.Holes);
			foreach (TurnCell anchor in data.Anchors)
			{
				if (TurnGrid.Contains(anchor.Cell) == false || holes.Contains(anchor.Cell))
				{
					throw new ArgumentException($"번갈아 {data.Id}: 그려진 칸 {anchor.Cell} 이 판에 없음");
				}
				anchors.Add(anchor.Cell, anchor.Aspect);
			}
		}

		/// <summary>욘이 고를 수 있는 성질</summary>
		public IReadOnlyList<int> Pool { get; }

		/// <summary>인형이 차례로 놓는 성질. 끝나면 처음부터</summary>
		public IReadOnlyList<int> Queue { get; }

		public IReadOnlyDictionary<int, int> Anchors => anchors;

		public bool IsHole(int cell) => holes.Contains(cell);

		public bool IsEmpty(int cell, IReadOnlyDictionary<int, int> cells) => TurnGrid.Contains(cell) && IsHole(cell) == false && cells.ContainsKey(cell) == false;

		public Dictionary<int, int> CellsWith(IReadOnlyList<TurnMove> moves)
		{
			Dictionary<int, int> cells = new Dictionary<int, int>(anchors);
			foreach (TurnMove move in moves)
			{
				cells.Add(move.Cell, move.Aspect);
			}
			return cells;
		}

		public HashSet<int> GroupFrom(int from, IReadOnlyDictionary<int, int> cells) => links.CollectGroup(from, cells, TurnGrid.Neighbors);

		public bool IsSolved(IReadOnlyDictionary<int, int> cells) => links.IsOneGroup(anchors.Keys, cells, TurnGrid.Neighbors);

		/// <summary>이웃 하나와는 붙는 빈칸에만</summary>
		public bool CanPut(int cell, int aspect, IReadOnlyDictionary<int, int> cells)
		{
			if (IsEmpty(cell, cells) == false)
			{
				return false;
			}
			foreach (int neighbor in TurnGrid.Neighbors(cell))
			{
				if (cells.TryGetValue(neighbor, out int found) && links.AreLinked(aspect, found))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>turn 번째 (0 부터) 에 인형이 놓을 성질</summary>
		public int DollAspect(int turn) => Queue[turn % Queue.Count];

		/// <summary>
		/// 욘이 놓을 때 이어지는 수 (욘, 그다음 인형). 욘의 수로 풀리면 인형은 안 놓음. 인형은 안 붙어도 놓고, 빈칸이 없으면 쉼.
		/// 놓을 수 있는지는 부른 쪽이 먼저 봄
		/// </summary>
		public List<TurnMove> Play(IReadOnlyList<TurnMove> moves, int cell, int aspect, ITurnHabit habit)
		{
			List<TurnMove> added = new List<TurnMove> { new TurnMove(cell, aspect, false) };
			Dictionary<int, int> cells = CellsWith(moves);
			cells.Add(cell, aspect);
			if (IsSolved(cells) == false && habit.TryChoose(this, cells, cell, out int chosen))
			{
				added.Add(new TurnMove(chosen, DollAspect(CountPlayerMoves(moves)), true));
			}
			return added;
		}

		public static int CountPlayerMoves(IReadOnlyList<TurnMove> moves)
		{
			int count = 0;
			foreach (TurnMove move in moves)
			{
				if (move.IsDoll == false)
				{
					count++;
				}
			}
			return count;
		}
	}
}
