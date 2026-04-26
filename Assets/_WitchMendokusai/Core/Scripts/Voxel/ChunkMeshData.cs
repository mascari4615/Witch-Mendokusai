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

		public void ApplyToMesh(Mesh mesh)
		{
			mesh.Clear();
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.vertices = Vertices;
			mesh.triangles = Triangles;
			mesh.colors = Colors;
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
		}
	}
}
