using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 복셀이 쓰는 것들의 <b>명시적 목록</b> — 블록 정의와 청크 재질 (TASK-WM-409).
	///
	/// ★ 왜 만들었나: 예전에는 <c>Resources.LoadAll("Blocks")</c> 로 <b>이름으로</b> 긁어 왔다.
	///   그러면 블록·재질·텍스처 배열이 <c>Resources/</c> 안에 있어야 하고,
	///   <c>Resources/</c> 안의 것은 <b>씬과 무관하게 모든 제품 빌드에</b> 실린다.
	///   실측 2026-08-16: 복셀을 한 조각도 안 쓰는 방치형 빌드에 `VoxelTextureArray` 1.3MB 가 들어갔다.
	///
	/// ★ 그래서 <b>참조</b>로 바꾼다. 이 목록을 <b>복셀을 실제로 쓰는 것</b>(Lab 스테이지의
	///   `ChunkManager`)이 들고 있으면, 복셀을 안 쓰는 제품에는 아무것도 안 실린다.
	///   판정 주체가 「폴더 이름」에서 「참조」로 옮겨간 것 — 그게 이 판의 요지다.
	/// </summary>
	[CreateAssetMenu(fileName = "BlockCatalog", menuName = "WM/Voxel/Block Catalog")]
	public sealed class BlockCatalog : ScriptableObject
	{
		[SerializeField] private BlockData[] blocks = new BlockData[0];
		[SerializeField] private Material chunkMaterial;

		public BlockData[] Blocks => blocks;
		public Material ChunkMaterial => chunkMaterial;

#if UNITY_EDITOR
		/// <summary>에디터에서 폴더를 훑어 목록을 채운다 — 손으로 끌어다 놓는 수고를 없앤다.</summary>
		public void EditorFill(BlockData[] found, Material material)
		{
			blocks = found;
			chunkMaterial = material;
			UnityEditor.EditorUtility.SetDirty(this);
		}
#endif
	}
}
