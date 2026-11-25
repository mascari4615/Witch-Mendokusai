using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>
	/// 육각 종이 한 장의 규칙. 그려진 성질 (Anchors) 을 전부 한 덩어리로 이으면 풀림.
	/// 놓은 것은 모름. 놓은 것을 든 쪽은 <see cref="HexPaperGame"/>
	/// </summary>
	public sealed class HexPaper
	{
		private readonly AspectLinks links;
		private readonly HashSet<HexCoord> cells = new HashSet<HexCoord>();
		private readonly Dictionary<HexCoord, int> anchors = new Dictionary<HexCoord, int>();

		public HexPaper(HexPaperData data, AspectLinks links)
		{
			this.links = links;
			Radius = data.Radius;
			Ink = data.Ink;

			for (int q = -data.Radius; q <= data.Radius; q++)
			{
				for (int r = -data.Radius; r <= data.Radius; r++)
				{
					HexCoord cell = new HexCoord(q, r);
					if (cell.Ring <= data.Radius)
					{
						cells.Add(cell);
					}
				}
			}
			foreach (HexSpot hole in data.Holes)
			{
				cells.Remove(hole.ToCoord());
			}
			foreach (HexAspect anchor in data.Anchors)
			{
				HexCoord cell = anchor.ToCoord();
				if (cells.Contains(cell) == false)
				{
					throw new ArgumentException($"종이 {data.Id}: 그려진 칸 {cell} 이 종이에 없음");
				}
				anchors.Add(cell, anchor.Aspect);
			}
		}

		public int Radius { get; }

		/// <summary>놓을 수 있는 성질 수</summary>
		public int Ink { get; }

		public IReadOnlyCollection<HexCoord> Cells => cells;
		public IReadOnlyDictionary<HexCoord, int> Anchors => anchors;

		public bool Contains(HexCoord cell) => cells.Contains(cell);

		/// <summary>그려진 것과 놓은 것을 합친 판</summary>
		public Dictionary<HexCoord, int> BoardWith(IReadOnlyDictionary<HexCoord, int> placed)
		{
			Dictionary<HexCoord, int> board = new Dictionary<HexCoord, int>(anchors);
			foreach (KeyValuePair<HexCoord, int> pair in placed)
			{
				board.Add(pair.Key, pair.Value);
			}
			return board;
		}

		public HashSet<HexCoord> GroupFrom(HexCoord from, IReadOnlyDictionary<HexCoord, int> board) => links.CollectGroup(from, board, cell => cell.Neighbors());

		public bool IsSolved(IReadOnlyDictionary<HexCoord, int> board) => links.IsOneGroup(anchors.Keys, board, cell => cell.Neighbors());
	}
}
