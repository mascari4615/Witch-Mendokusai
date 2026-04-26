namespace WitchMendokusai
{
	/// <summary>
	/// Voxel 시스템 상수. 청크 크기 변경 시 모든 데이터 호환성 깨지므로 신중하게.
	/// </summary>
	public static class VoxelConstants
	{
		public const int CHUNK_SIZE_X = 16;
		public const int CHUNK_SIZE_Y = 64;
		public const int CHUNK_SIZE_Z = 16;
		public const int CHUNK_VOLUME = CHUNK_SIZE_X * CHUNK_SIZE_Y * CHUNK_SIZE_Z;

		/// <summary>Air 블록의 namespaced identifier. RuntimeId는 BlockRegistry가 0으로 보장.</summary>
		public const string AIR_IDENTIFIER = "wm:air";

		/// <summary>Air 블록의 RuntimeId. BlockRegistry가 항상 0번에 Air를 등록한다.</summary>
		public const ushort AIR_RUNTIME_ID = 0;

		public const string IDENTIFIER_NAMESPACE = "wm";

		public static int Index(int x, int y, int z)
			=> (y * CHUNK_SIZE_X * CHUNK_SIZE_Z) + (z * CHUNK_SIZE_X) + x;

		public static bool IsInBounds(int x, int y, int z)
			=> x >= 0 && x < CHUNK_SIZE_X && y >= 0 && y < CHUNK_SIZE_Y && z >= 0 && z < CHUNK_SIZE_Z;
	}
}
