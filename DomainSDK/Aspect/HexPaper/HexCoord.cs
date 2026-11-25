using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Aspect
{
	/// <summary>육각 칸의 축 좌표 (q, r). 셋째 축은 -q-r</summary>
	public readonly struct HexCoord : IEquatable<HexCoord>
	{
		private static readonly (int Q, int R)[] DIRECTIONS = { (1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1) };

		public readonly int Q;
		public readonly int R;

		public HexCoord(int q, int r)
		{
			Q = q;
			R = r;
		}

		/// <summary>가운데에서 떨어진 칸 수</summary>
		public int Ring => Math.Max(Math.Abs(Q), Math.Max(Math.Abs(R), Math.Abs(Q + R)));

		public IEnumerable<HexCoord> Neighbors()
		{
			foreach ((int q, int r) in DIRECTIONS)
			{
				yield return new HexCoord(Q + q, R + r);
			}
		}

		public bool Equals(HexCoord other) => Q == other.Q && R == other.R;

		public override bool Equals(object other) => other is HexCoord coord && Equals(coord);

		public override int GetHashCode() => (Q * 397) ^ R;

		public override string ToString() => $"({Q}, {R})";
	}
}
