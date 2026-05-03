using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	[ExecuteAlways]
	public class VoxelTestRunner : MonoBehaviour
	{
		[SerializeField] private ChunkPosition chunkPosition = new(0, 0);

		private void Start()
		{
			if (Application.isPlaying)
			{
				Generate();
			}
		}

		[ContextMenu("Generate Mesh")]
		public void Generate()
		{
			TerrainParameters terrainParameters = TerrainParametersService.Active;
			if (terrainParameters == null)
			{
				Debug.LogWarning($"[VoxelTestRunner] Active TerrainParameters 없음 — Resources/{TerrainParametersService.ACTIVE_RESOURCE_PATH} 확인.");
				return;
			}

			if (BlockRegistry.IsInitialized == false)
			{
				BlockBootstrap.Reload();
			}

			Chunk chunk = new(chunkPosition);
			ChunkGenerator.Generate(chunk, terrainParameters);

			ChunkMeshData meshData = ChunkMesher.GenerateMeshData(chunk);

			MeshFilter filter = GetComponent<MeshFilter>();
			if (filter.sharedMesh == null)
				filter.sharedMesh = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
			
			meshData.ApplyToMesh(filter.sharedMesh);

			// 생성된 단일 청크가 플레이어 발밑(Y=0 기준)에 깔리도록 오프셋 적용
			transform.position = new Vector3(transform.position.x, -(VoxelConstants.CHUNK_SIZE_Y / 2f), transform.position.z);

			MeshCollider collider = GetComponent<MeshCollider>();
			if (collider == null)
				collider = gameObject.AddComponent<MeshCollider>();
			
			collider.sharedMesh = null;
			collider.sharedMesh = filter.sharedMesh;

			if (GetComponent<GroundSurface>() == null)
				gameObject.AddComponent<GroundSurface>();
		}
	}
}
