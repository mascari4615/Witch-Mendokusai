using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 메인 스레드 없이 텍스처/메시 정보를 보관하는 순수 데이터 컨테이너
	/// 백그라운드 스레드에서 생성 후 메인 스레드에 전달됩니다.
	/// </summary>
	public class ChunkMeshData
	{
		public Vector3[] Vertices;
		public int[] Triangles;
		public Color[] Colors;
		public Vector2[] Uvs;
		/// <summary>UV 채널 1 — atlas tile rect + worldScale 묶음. (xMin, yMin, atlasSize, worldScale).
		/// atlasSize == 0 = sentinel (텍스쳐 미할당 → 셰이더 vertex color path).</summary>
		public Vector4[] TileRects;

		public void ApplyToMesh(Mesh mesh)
		{
			mesh.Clear();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.vertices = Vertices;
			mesh.triangles = Triangles;
			mesh.colors = Colors;
			mesh.uv = Uvs;
			mesh.SetUVs(1, TileRects);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
		}
	}
}
