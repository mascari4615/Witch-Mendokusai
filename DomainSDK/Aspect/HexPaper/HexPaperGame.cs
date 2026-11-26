using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>육각 종이를 푸는 한 판. 놓은 것만 들고 판은 그때그때 종이와 합침</summary>
	public sealed class HexPaperGame
	{
		private readonly Dictionary<HexCoord, int> placed = new Dictionary<HexCoord, int>();

		public HexPaperGame(HexPaper paper)
		{
			Paper = paper;
		}

		public HexPaper Paper { get; }
		public int InkLeft => Paper.Ink - placed.Count;
		public bool IsSolved => Paper.IsSolved(Board);
		public Dictionary<HexCoord, int> Board => Paper.BoardWith(placed);

		public bool IsPlaced(HexCoord cell) => placed.ContainsKey(cell);

		/// <summary>붙지 않는 자리에도 놓을 수 있음. 빈칸과 잉크만 봄</summary>
		public bool CanPlace(HexCoord cell) => InkLeft > 0 && Paper.Contains(cell) && Paper.Anchors.ContainsKey(cell) == false && IsPlaced(cell) == false && IsSolved == false;

		public bool TryPlace(HexCoord cell, int aspect)
		{
			if (CanPlace(cell) == false)
			{
				return false;
			}
			placed.Add(cell, aspect);
			return true;
		}

		/// <summary>놓은 것을 지우면 잉크가 돌아옴. 다 이은 종이는 그대로</summary>
		public bool TryErase(HexCoord cell) => IsSolved == false && placed.Remove(cell);

		public void Clear() => placed.Clear();
	}
}
