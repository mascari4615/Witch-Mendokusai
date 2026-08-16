using UnityEngine;

namespace WitchMendokusai
{
	[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
	[ExecuteAlways]
	public class VoxelTestRunner : MonoBehaviour
	{
		/// <summary>복셀이 쓰는 것들의 목록 — 이 참조가 「복셀을 쓴다」는 선언이다 (TASK-WM-409).</summary>
		[SerializeField] private BlockCatalog catalog;
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
			terrainParameters.EnsureHeightmapCache(); // main thread 1회 캐시 — heightmap PNG 옵션 사용 시 필요

			if (BlockRegistry.IsInitialized == false)
			{
				// 이름으로 긁어 오던 것을 <b>참조</b>로 바꿨다 (TASK-WM-409) — 카탈로그가 없으면 그렇다고 말한다.
				if (catalog == null)
				{
					Debug.LogError("[VoxelTestRunner] BlockCatalog 이 안 꽂혔다 — 블록을 못 읽는다 (TASK-WM-409)");
					return;
				}
				BlockBootstrap.Load(catalog.Blocks);
			}

			Chunk chunk = new(chunkPosition);
			ChunkGenerator.Generate(chunk, terrainParameters);

			ChunkMeshData meshData = ChunkMesher.GenerateMeshData(chunk, terrainParameters);

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
