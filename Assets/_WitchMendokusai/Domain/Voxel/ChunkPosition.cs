using System;

namespace WitchMendokusai
{
	/// <summary>
	/// 청크 좌표 (X, Z). Y stacking은 미래 확장. 청크 lookup의 키.
	/// </summary>
	[Serializable]
	public struct ChunkPosition : IEquatable<ChunkPosition>
	{
		public int X;
		public int Z;

		public ChunkPosition(int x, int z)
		{
			X = x;
			Z = z;
		}

		public bool Equals(ChunkPosition other) => X == other.X && Z == other.Z;
		public override bool Equals(object obj) => obj is ChunkPosition other && Equals(other);
		public override int GetHashCode() => unchecked((X * 397) ^ Z);
		public override string ToString() => $"({X}, {Z})";

		public static bool operator ==(ChunkPosition a, ChunkPosition b) => a.Equals(b);
		public static bool operator !=(ChunkPosition a, ChunkPosition b) => a.Equals(b) == false;
	}
}
