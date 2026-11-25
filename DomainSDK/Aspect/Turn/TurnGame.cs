using System;
using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>번갈아를 두는 한 판. 놓은 수의 목록과 수 제한만 듦</summary>
	public sealed class TurnGame
	{
		private readonly List<TurnMove> moves = new List<TurnMove>();
		private readonly ITurnHabit habit;

		public TurnGame(TurnBoard board, ITurnHabit habit, int limit)
		{
			Board = board;
			this.habit = habit;
			Limit = limit;
		}

		public TurnBoard Board { get; }
		public int Limit { get; }
		public IReadOnlyList<TurnMove> Moves => moves;
		public int MovesLeft => Limit - TurnBoard.CountPlayerMoves(moves);
		public Dictionary<int, int> Cells => Board.CellsWith(moves);
		public bool IsSolved => Board.IsSolved(Cells);

		/// <summary>이번 차례부터 ahead 번째에 인형이 놓을 성질 (0 이 이번)</summary>
		public int DollAspectAhead(int ahead) => Board.DollAspect(TurnBoard.CountPlayerMoves(moves) + ahead);

		public bool CanPut(int cell, int aspect)
		{
			if (MovesLeft <= 0 || Board.Pool.Contains(aspect) == false)
			{
				return false;
			}
			Dictionary<int, int> cells = Cells;
			return Board.IsSolved(cells) == false && Board.CanPut(cell, aspect, cells);
		}

		/// <summary>놓으면 이어질 수. 판은 그대로. 못 놓으면 빈 목록</summary>
		public IReadOnlyList<TurnMove> Preview(int cell, int aspect)
		{
			if (CanPut(cell, aspect) == false)
			{
				return Array.Empty<TurnMove>();
			}
			return Board.Play(moves, cell, aspect, habit);
		}

		public bool TryPut(int cell, int aspect)
		{
			IReadOnlyList<TurnMove> added = Preview(cell, aspect);
			moves.AddRange(added);
			return added.Count > 0;
		}

		/// <summary>욘의 마지막 수와 거기 이어진 인형의 수를 무름</summary>
		public bool TryUndo()
		{
			int index = moves.FindLastIndex(move => move.IsDoll == false);
			if (index < 0)
			{
				return false;
			}
			moves.RemoveRange(index, moves.Count - index);
			return true;
		}

		public void Clear() => moves.Clear();
	}
}
