using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 청크 GameObject의 생성과 파괴에 따른 GC 스파이크를 막기 위한 오브젝트 풀
	/// Mesh 객체 자체도 재사용합니다.
	/// </summary>
	public class ChunkPool : MonoBehaviour
	{
		public const string DEFAULT_MATERIAL_RESOURCE = "VoxelMaterial";

		[SerializeField] private Material chunkMaterial;

		private readonly Stack<GameObject> pool = new();

		private void Awake()
		{
			if (chunkMaterial == null)
				chunkMaterial = Resources.Load<Material>(DEFAULT_MATERIAL_RESOURCE);
			if (chunkMaterial == null)
				Debug.LogError($"[ChunkPool] {DEFAULT_MATERIAL_RESOURCE} 머티리얼을 찾지 못했다. WitchMendokusai/Voxel/Generate Default Material 메뉴 호출.");
		}

		public GameObject Get(ChunkPosition position)
		{
			GameObject chunkGo;
			if (pool.Count > 0)
			{
				chunkGo = pool.Pop();
				chunkGo.SetActive(true);
			}
			else
			{
				chunkGo = new GameObject();
				chunkGo.transform.SetParent(transform);
				
				MeshFilter filter = chunkGo.AddComponent<MeshFilter>();
				// 메쉬 객체도 미리 생성해두고 재사용합니다.
				filter.sharedMesh = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
				
				MeshRenderer renderer = chunkGo.AddComponent<MeshRenderer>();
				if (chunkMaterial != null)
					renderer.sharedMaterial = chunkMaterial;

				chunkGo.AddComponent<MeshCollider>();
				chunkGo.AddComponent<GroundSurface>();
			}

			chunkGo.name = $"Chunk_{position.X}_{position.Z}";
			chunkGo.transform.position = new Vector3(
				position.X * VoxelConstants.CHUNK_SIZE_X,
				-(VoxelConstants.CHUNK_SIZE_Y / 2f),
				position.Z * VoxelConstants.CHUNK_SIZE_Z
			);

			return chunkGo;
		}

		public void Release(GameObject chunkGo)
		{
			if (chunkGo == null)
				return;

			chunkGo.SetActive(false);
			chunkGo.name = "Chunk_Pooled";
			
			MeshCollider collider = chunkGo.GetComponent<MeshCollider>();
			if (collider != null)
			{
				collider.sharedMesh = null;
			}

			MeshFilter filter = chunkGo.GetComponent<MeshFilter>();
			if (filter != null && filter.sharedMesh != null)
			{
				filter.sharedMesh.Clear();
			}

			pool.Push(chunkGo);
		}
	}
}
